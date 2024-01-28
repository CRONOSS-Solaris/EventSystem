using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Torch;
using VRage;
using VRage.Game;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace EventSystem.Serialization
{
    public class GridSpawner
    {
        public readonly Logger Log = LogManager.GetLogger("EventSystem/GridSpawner");
        private long DefaultNewOwner = 144115188075855881;
        private HashSet<MyCubeGrid> _spawnedGrids = new HashSet<MyCubeGrid>();

        public GridSpawner()
        {
        }

        public async Task<bool> SpawnGrids(IEnumerable<MyObjectBuilder_CubeGrid> grids, Vector3D position)
        {
            bool gridsSpawned = false;

            await TorchBase.Instance.InvokeAsync(() =>
            {
                var entityBaseGrids = grids.Cast<MyObjectBuilder_EntityBase>().ToList();
                MyEntities.RemapObjectBuilderCollection(entityBaseGrids);

                foreach (var gridBuilder in entityBaseGrids.Cast<MyObjectBuilder_CubeGrid>())
                {
                    try
                    {
                        // Ajustowanie pozycji spawnu nad terenem
                        Vector3D adjustedPosition = AdjustPositionAboveTerrain(position, gridBuilder);

                        float naturalGravityInterference;
                        var gravityVector = MyAPIGateway.Physics.CalculateNaturalGravityAt(adjustedPosition, out naturalGravityInterference);
                        gravityVector.Normalize();
                        var up = -gravityVector;
                        var forward = Vector3D.CalculatePerpendicularVector(gravityVector);

                        gridBuilder.PositionAndOrientation = new MyPositionAndOrientation
                        {
                            Position = adjustedPosition,
                            Forward = (Vector3)forward,
                            Up = (Vector3)up
                        };

                        TransferGridOwnership(new[] { gridBuilder }, DefaultNewOwner);
                        EnableRequiredItemsOnLoad(gridBuilder);

                        // Tworzenie obiektu w grze
                        IMyEntity entity = MyAPIGateway.Entities.CreateFromObjectBuilderParallel(gridBuilder, false, Increment);
                        gridsSpawned = true;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Exception occurred while spawning grid at {position}: {ex.Message}");
                    }
                }
            });

            return gridsSpawned;
        }

        private double CalculateGridSize(MyObjectBuilder_CubeGrid gridBuilder)
        {
            Vector3I min = Vector3I.MaxValue;
            Vector3I max = Vector3I.MinValue;

            // Obliczanie minimalnych i maksymalnych punktów siatki
            foreach (var block in gridBuilder.CubeBlocks)
            {
                var blockDefinition = MyDefinitionManager.Static.GetCubeBlockDefinition(block.GetId());
                var blockSize = blockDefinition.Size;

                Vector3I blockMax = block.Min + blockSize - Vector3I.One;
                min = Vector3I.Min(min, block.Min);
                max = Vector3I.Max(max, blockMax);
            }

            Vector3I gridSize = max - min + Vector3I.One;

            // Obliczanie maksymalnego wymiaru siatki
            int maxDimension = Math.Max(Math.Max(gridSize.X, gridSize.Y), gridSize.Z);

            // Zwracanie wielkości sześcianu, który opisuje siatkę
            return maxDimension * MyDefinitionManager.Static.GetCubeSize(gridBuilder.GridSizeEnum);
        }


        private Vector3D AdjustPositionAboveTerrain(Vector3D position, MyObjectBuilder_CubeGrid gridBuilder)
        {
            // Pobierz najbliższą planetę dla danej pozycji
            var closestPlanet = MyGamePruningStructure.GetClosestPlanet(position);
            if (closestPlanet != null)
            {
                // Znajdź najbliższy punkt na powierzchni planety
                Vector3D closestSurfacePoint = closestPlanet.GetClosestSurfacePointGlobal(position);

                return new Vector3D(position.X, closestSurfacePoint.Y, position.Z);
            }

            return position;
        }

        private void TransferGridOwnership(IEnumerable<MyObjectBuilder_CubeGrid> grids, long newOwner)
        {
            try
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
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred during TransferGridOwnership.");
            }
        }

        public void Increment(IMyEntity entity)
        {
            try
            {
                var grid = entity as MyCubeGrid;
                if (grid != null)
                {
                    _spawnedGrids.Add(grid);
                    grid.Physics.LinearVelocity = Vector3.Zero;
                    grid.Physics.AngularVelocity = Vector3.Zero;
                    MyAPIGateway.Entities.AddEntity(grid, true);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred in Increment method.");
            }
        }

        private void EnableRequiredItemsOnLoad(MyObjectBuilder_CubeGrid grid)
        {
            try
            {
                foreach (var block in grid.CubeBlocks)
                {
                    if (block is MyObjectBuilder_FunctionalBlock functionalBlock)
                    {
                        functionalBlock.Enabled = true;
                    }
                }

                if (grid is MyObjectBuilder_CubeGrid gridBlock)
                {
                    gridBlock.DampenersEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred in EnableRequiredItemsOnLoad.");
            }
        }
    }
}
