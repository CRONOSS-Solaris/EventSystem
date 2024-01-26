

using NLog;
using Sandbox.Common.ObjectBuilders;
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
                foreach (var gridBuilder in grids)
                {
                    try
                    {

                        float naturalGravityInterference;
                        var gravityVector = MyAPIGateway.Physics.CalculateNaturalGravityAt(position, out naturalGravityInterference);
                        gravityVector.Normalize();

                        var up = -gravityVector;
                        var forward = Vector3D.CalculatePerpendicularVector(gravityVector);


                        gridBuilder.PositionAndOrientation = new MyPositionAndOrientation
                        {
                            Position = position,
                            Forward = (Vector3)forward,
                            Up = (Vector3)up
                        };

                        TransferGridOwnership(new[] { gridBuilder }, DefaultNewOwner);
                        EnableRequiredItemsOnLoad(gridBuilder);

                        MyEntities.RemapObjectBuilderCollection(grids);

                        foreach (var o in grids)
                        {
                            IMyEntity entity = MyAPIGateway.Entities.CreateFromObjectBuilderParallel(o, false, Increment);
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
