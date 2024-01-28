using EventSystem.Utils;
using NLog;
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

        private MyObjectBuilder_CubeGrid _currentGridBuilder;

        public GridSpawner()
        {
        }

        public async Task<bool> SpawnGrids(IEnumerable<MyObjectBuilder_CubeGrid> grids, Vector3D position)
        {
            bool gridsSpawned = false;
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Starting to spawn grids at {position}");

            position = CorrectSpawnPosition(position);
            if (position == Vector3D.Zero) return false;

            await GameEvents.InvokeActionAsync(() => ProcessGrids(grids, position, out gridsSpawned));

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Grid spawn process completed. Success: {gridsSpawned}");
            return gridsSpawned;
        }

        private Vector3D CorrectSpawnPosition(Vector3D position)
        {
            return CorrectPositionIfInsidePlanetOrVoxel(position, 100.0);
        }

        private void ProcessGrids(IEnumerable<MyObjectBuilder_CubeGrid> grids, Vector3D position, out bool gridsSpawned)
        {
            gridsSpawned = false;
            var entityBaseGrids = grids.Cast<MyObjectBuilder_EntityBase>().ToList();
            MyEntities.RemapObjectBuilderCollection(entityBaseGrids);

            foreach (var gridBuilder in entityBaseGrids.Cast<MyObjectBuilder_CubeGrid>())
            {
                _currentGridBuilder = gridBuilder;
                if (!TrySpawnGrid(position)) return;
                gridsSpawned = true;
            }
        }

        private bool TrySpawnGrid(Vector3D position)
        {
            try
            {
                SetGridPositionAndOrientation(position);
                TransferGridOwnership(new[] { _currentGridBuilder }, DefaultNewOwner);
                EnableRequiredItemsOnLoad(_currentGridBuilder);
                SpawnEntity(_currentGridBuilder);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Exception occurred while spawning grid: {ex.Message}");
                return false;
            }
        }

        private void SetGridPositionAndOrientation(Vector3D position)
        {
            float naturalGravityInterference;
            var gravityVector = MyAPIGateway.Physics.CalculateNaturalGravityAt(position, out naturalGravityInterference);
            gravityVector.Normalize();
            var up = -gravityVector;
            var forward = Vector3D.CalculatePerpendicularVector(gravityVector);

            _currentGridBuilder.PositionAndOrientation = new MyPositionAndOrientation
            {
                Position = position,
                Forward = (Vector3)forward,
                Up = (Vector3)up
            };
        }

        private void SpawnEntity(MyObjectBuilder_CubeGrid gridBuilder)
        {
            IMyEntity entity = MyAPIGateway.Entities.CreateFromObjectBuilderParallel(gridBuilder, false, Increment);
        }

        private Vector3D CorrectPositionIfInsidePlanetOrVoxel(Vector3D position, double safetyDistance)
        {
            MyPlanet closestPlanet = MyGamePruningStructure.GetClosestPlanet(position);
            if (closestPlanet != null)
            {
                Vector3D closestSurfacePoint = closestPlanet.GetClosestSurfacePointGlobal(position);
                double distanceToSurface = Vector3D.Distance(position, closestSurfacePoint);
                if (distanceToSurface < safetyDistance)
                {
                    return new Vector3D(position.X, closestSurfacePoint.Y + safetyDistance, position.Z);
                }
            }
            return position;
        }

        private void TransferGridOwnership(IEnumerable<MyObjectBuilder_CubeGrid> grids, long newOwner)
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

        private void EnableRequiredItemsOnLoad(MyObjectBuilder_CubeGrid grid)
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

        public void Increment(IMyEntity entity)
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
    }
}
