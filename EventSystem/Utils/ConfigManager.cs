﻿using NLog;
using System;
using System.IO;
using Torch;

namespace EventSystem.Utils
{
    public class ConfigManager
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly string _rootPath;


        public ConfigManager(string rootPath)
        {
            _rootPath = rootPath;
            CreateFolders(Path.Combine(_rootPath, "Config"),
                          Path.Combine(_rootPath, "Event"),
                          Path.Combine(_rootPath, "PlayerAccounts"),
                          Path.Combine(_rootPath, "PrefabBuildBattle"));
        }


        public void CreateFolders(params string[] folderPaths)
        {
            foreach (var path in folderPaths)
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    Log.Info($"Folder created: {path}");
                }
                else
                {
                    Log.Info($"Folder already exists: {path}");
                }
            }
        }

        public void CreateFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                using (File.Create(filePath)) { }
                Log.Info($"Created file: {filePath}");
            }
            else
            {
                Log.Info($"File already exists: {filePath}");
            }
        }


        public Persistent<T> SetupConfig<T>(string fileName, T defaultConfig) where T : new()
        {
            var configFolderPath = Path.Combine(_rootPath, "Config");
            var configFilePath = Path.Combine(configFolderPath, fileName);

            Persistent<T> config;

            try
            {
                config = Persistent<T>.Load(configFilePath);
            }
            catch (Exception e)
            {
                Log.Warn(e);
                config = new Persistent<T>(configFilePath, defaultConfig);
            }

            if (config.Data == null)
            {
                Log.Info($"Creating default config for {fileName} because none was found!");
                config = new Persistent<T>(configFilePath, defaultConfig);
                config.Save();
            }

            return config;
        }
    }
}
