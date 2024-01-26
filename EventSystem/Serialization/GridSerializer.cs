using NLog;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Torch;
using VRage;
using VRage.Game;
using VRage.ObjectBuilders.Private;
using VRageMath;

namespace EventSystem.Serialization
{
    public static class GridSerializer
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/GridSerializer");
        private static long DefaultNewOwner = 144115188075855881;

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
                        return false;
                    }

                    ResetGear(grids);
                    return await SpawnGrids(grids, position);
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

        private static async Task<bool> SpawnGrids(IEnumerable<MyObjectBuilder_CubeGrid> grids, Vector3D position)
        {
            bool gridsSpawned = false;

            await TorchBase.Instance.InvokeAsync(() =>
            {
                foreach (var gridBuilder in grids)
                {
                    try
                    {
                        // Ustawienie pozycji spawnu dla siatki
                        gridBuilder.PositionAndOrientation = new MyPositionAndOrientation
                        {
                            Position = position
                        };

                        // Transfer własności siatek
                        TransferGridOwnership(new[] { gridBuilder }, DefaultNewOwner);

                        // Tworzenie siatki w grze
                        var spawnedGrid = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridBuilder) as MyCubeGrid;

                        if (spawnedGrid != null)
                        {
                            Log.Info($"Grid {spawnedGrid.DisplayName} spawned successfully at {position}.");
                            gridsSpawned = true;
                        }
                        else
                        {
                            Log.Warn($"Failed to spawn grid at {position}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Exception occurred while spawning grid at {position}: {ex.Message}");
                    }
                }
            });

            return gridsSpawned;
        }

        private static void TransferGridOwnership(IEnumerable<MyObjectBuilder_CubeGrid> grids, long newOwner)
        {
            foreach (var grid in grids)
            {
                foreach (var block in grid.CubeBlocks)
                {
                    block.Owner = newOwner;
                    block.BuiltBy = newOwner;
                }
            }
        }
    }
}