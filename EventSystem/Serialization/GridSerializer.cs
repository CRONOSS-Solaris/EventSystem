using NLog;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using VRage.Game;
using VRage.ObjectBuilders.Private;
using VRageMath;
using System.IO;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using EventSystem.Serialization;
using EventSystem;
using EventSystem.Utils;

public static class GridSerializer
{
    public static readonly Logger Log = LogManager.GetLogger("EventSystem/GridSerializer");

    public static async Task<bool> LoadAndSpawnGrid(string folderPath, string gridName, Vector3D position)
    {
        string filePath = Path.Combine(folderPath, gridName + ".sbc");
        LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Loading grid file: {filePath}");

        if (!File.Exists(filePath))
        {
            Log.Error($"Grid file does not exist at path: {filePath}");
            return false;
        }

        try
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Deserializing grid file...");
            if (MyObjectBuilderSerializerKeen.DeserializeXML(filePath, out MyObjectBuilder_Definitions definitions))
            {
                LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Grid file deserialized successfully.");

                if (!TryGetGridsFromDefinition(definitions, out IEnumerable<MyObjectBuilder_CubeGrid> grids))
                {
                    Log.Error("Failed to get grids from definition.");
                    return false;
                }

                LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Spawning grid(s) at position {position}");
                GridSpawner spawner = new GridSpawner();
                bool result = await spawner.SpawnGrids(grids, position);
                LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Grid(s) spawn at position {position} completed with status: {result}");
                return result;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to deserialize grid file at path: {filePath}");
        }

        return false;
    }

    private static bool TryGetGridsFromDefinition(MyObjectBuilder_Definitions definitions, out IEnumerable<MyObjectBuilder_CubeGrid> grids)
    {
        grids = new List<MyObjectBuilder_CubeGrid>();
        if (definitions.ShipBlueprints != null && definitions.ShipBlueprints.Any())
        {
            grids = definitions.ShipBlueprints.SelectMany(blueprint => blueprint.CubeGrids);
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Grids successfully extracted from ship blueprints.");
            return true;
        }

        Log.Error("Invalid MyObjectBuilder_Definitions, no ship blueprints found.");
        return false;
    }
}
