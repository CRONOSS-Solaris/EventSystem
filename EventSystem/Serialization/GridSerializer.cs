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

public static class GridSerializer
{
    public static readonly Logger Log = LogManager.GetLogger("EventSystem/GridSerializer");

    public static async Task<bool> LoadAndSpawnGrid(string folderPath, string gridName, Vector3D position)
    {
        string filePath = Path.Combine(folderPath, gridName + ".sbc");
        if (!File.Exists(filePath))
        {
            Log.Error($"Grid file does not exist at path: {filePath}");
            return false;
        }

        try
        {
            if (MyObjectBuilderSerializerKeen.DeserializeXML(filePath, out MyObjectBuilder_Definitions definitions))
            {
                if (!TryGetGridsFromDefinition(definitions, out IEnumerable<MyObjectBuilder_CubeGrid> grids))
                {
                    Log.Error("Failed to get grids from definition.");
                    return false;
                }

                ResetGear(grids);
                GridSpawner spawner = new GridSpawner();
                return await spawner.SpawnGrids(grids, position);
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
            return true;
        }

        Log.Error("Invalid MyObjectBuilder_Definitions, no ship blueprints found.");
        return false;
    }

    public static void ResetGear(IEnumerable<MyObjectBuilder_CubeGrid> grids)
    {
        foreach (var grid in grids)
        {
            foreach (var block in grid.CubeBlocks.OfType<MyObjectBuilder_LandingGear>())
            {
                block.IsLocked = false;
                block.AutoLock = true;
                block.FirstLockAttempt = false;
                block.AttachedEntityId = null;
                block.MasterToSlave = null;
                block.GearPivotPosition = null;
                block.OtherPivot = null;
                block.LockMode = SpaceEngineers.Game.ModAPI.Ingame.LandingGearMode.Unlocked;
            }
        }
    }
}
