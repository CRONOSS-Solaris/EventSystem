using EventSystem;
using EventSystem.Serialization;
using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VRage.Game;
using VRage.ObjectBuilders.Private;
using VRageMath;

public static class GridSerializer
{
    public static readonly Logger Log = LogManager.GetLogger("EventSystem/GridSerializer");

    public static async Task<HashSet<long>> LoadAndSpawnGrid(string folderPath, string gridName, Vector3D position)
    {
        string filePath = Path.Combine(folderPath, gridName + ".sbc");
        LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Loading grid file: {filePath}");

        if (!File.Exists(filePath))
        {
            Log.Error($"Grid file does not exist at path: {filePath}");
            return new HashSet<long>();
        }

        try
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Deserializing grid file...");
            if (MyObjectBuilderSerializerKeen.DeserializeXML(filePath, out MyObjectBuilder_Definitions? definitions))
            {
                LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Grid file deserialized successfully.");

                List<MyObjectBuilder_CubeGrid> grids = GetGridsFromDefinition(definitions);
                if (grids.Any())
                {
                    GridSpawner spawner = new GridSpawner();
                    var entityIds = await spawner.SpawnGrids(grids, position);
                    LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Grid(s) spawn at position {position} completed.");
                    return entityIds;
                }
                else
                {
                    Log.Error("No grids found in definition.");
                }
            }
            else
            {
                Log.Error("Failed to deserialize the grid file.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to deserialize grid file at path: {filePath}");
        }

        return new HashSet<long>();
    }

    private static List<MyObjectBuilder_CubeGrid> GetGridsFromDefinition(MyObjectBuilder_Definitions definitions)
    {
        var grids = new List<MyObjectBuilder_CubeGrid>();

        if (definitions.ShipBlueprints != null)
        {
            foreach (var blueprint in definitions.ShipBlueprints)
            {
                grids.AddRange(blueprint.CubeGrids);
            }
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Grids extracted from ship blueprints.");
        }

        if (definitions.Prefabs != null)
        {
            foreach (var prefab in definitions.Prefabs)
            {
                grids.AddRange(prefab.CubeGrids);
            }
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Grids extracted from prefabs.");
        }

        return grids;
    }
}
