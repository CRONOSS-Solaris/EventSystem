using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Torch.API.Managers;
using Torch.API.Session;
using Torch.Commands;
using Torch.Managers.PatchManager;

namespace EventSystem.Utils
{
    public static class Compiler
    {
        public static bool Compile(string folder, ITorchSession session) // Teraz przyjmuje sesję jako argument
        {
            return CompileFromFile(folder, session);
        }

        public static MetadataReference[] GetRequiredRefernces()
        {
            List<MetadataReference> metadataReferenceList = new List<MetadataReference>();
            foreach (Assembly assembly in ((IEnumerable<Assembly>)AppDomain.CurrentDomain.GetAssemblies()).Where<Assembly>((Func<Assembly, bool>)(a => !a.IsDynamic)))
            {
                if (!assembly.IsDynamic && assembly.Location != null & string.Empty != assembly.Location)
                    metadataReferenceList.Add((MetadataReference)MetadataReference.CreateFromFile(assembly.Location));
            }

            foreach (var filePath in Directory.GetFiles($"{EventSystemMain.basePath}/{EventSystemMain.PluginName}/").Where(x => x.Contains(".dll")))
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    metadataReferenceList.Add(MetadataReference.CreateFromStream(fileStream));
                }
            }

            foreach (var filePath in Directory.GetFiles(EventSystemMain.path).Where(x => x.Contains(".dll")))
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    metadataReferenceList.Add(MetadataReference.CreateFromStream(fileStream));
                }
            }

            return metadataReferenceList.ToArray();
        }
        private static bool CompileFromFile(string folder, ITorchSession session)
        {
            var patches = session.Managers.GetManager<PatchManager>();
            var commands = session.Managers.GetManager<CommandManager>();
            List<SyntaxTree> trees = new List<SyntaxTree>();

            try
            {
                foreach (var filePath in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(x => x.EndsWith(".cs")))
                {
                    using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (StreamReader streamReader = new StreamReader(fileStream))
                        {
                            string text = streamReader.ReadToEnd();
                            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(text);
                            trees.Add(syntaxTree);
                        }
                    }
                }

            }
            catch (Exception e)
            {
                EventSystemMain.Log.Error($"Compiler file error {e}");
            }
            var compilation = CSharpCompilation.Create("MyAssembly")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(GetRequiredRefernces()) // Add necessary references
                .AddSyntaxTrees(trees);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                var result = compilation.Emit(memoryStream);

                if (result.Success)
                {
                    Assembly assembly = Assembly.Load(memoryStream.ToArray());
                    EventSystemMain.Log.Error("Compilation successful!");
                    EventSystemMain.myAssemblies.Add(assembly);

                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            EventSystemMain.Log.Info($"Loaded type: {type.FullName}");

                            MethodInfo method = type.GetMethod("Patch", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                            if (method == null)
                            {
                                continue;
                            }
                            ParameterInfo[] ps = method.GetParameters();
                            if (ps.Length != 1 || ps[0].IsOut || ps[0].IsOptional || ps[0].ParameterType.IsByRef ||
                                ps[0].ParameterType != typeof(PatchContext) || method.ReturnType != typeof(void))
                            {
                                continue;
                            }
                            var context = patches.AcquireContext();
                            method.Invoke(null, new object[] { context });
                        }
                        patches.Commit();
                        foreach (var obj in assembly.GetTypes())
                        {
                            commands.RegisterCommandModule(obj);
                        }
                    }
                    catch (Exception e)
                    {
                        EventSystemMain.Log.Error($"{e}");
                    }
                }
                else
                {
                    Console.WriteLine("Compilation failed:");
                    EventSystemMain.CompileFailed = true;
                    foreach (var diagnostic in result.Diagnostics)
                    {
                        EventSystemMain.Log.Error(diagnostic);
                    }

                    return true;
                }
            }
            EventSystemMain.CompileFailed = false;
            return true;
        }
    }
}
