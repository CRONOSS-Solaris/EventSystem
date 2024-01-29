using EventSystem.Utils;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VRage;
using VRage.Game;
using VRage.ModAPI;
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
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Starting to spawn grids at {position}");

            position = CorrectSpawnPosition(position);
            if (position == Vector3D.Zero) return false;

            ProcessGrids(grids, position);
            await GameEvents.InvokeActionAsync(() => SpawnEntities(grids));

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Grid spawn process completed. Success: {gridsSpawned}");
            return gridsSpawned;
        }

        private Vector3D CorrectSpawnPosition(Vector3D position)
        {
            var naturalGravity = MyAPIGateway.Physics.CalculateNaturalGravityAt(position, out _);
            if (naturalGravity.LengthSquared() < 0.01)
            {
                return position; // W kosmosie
            }
            else
            {
                return CorrectPositionIfInsidePlanetOrVoxel(position, 100.0); // Na planecie
            }
        }

        private void ProcessGrids(IEnumerable<MyObjectBuilder_CubeGrid> grids, Vector3D newPosition)
        {
            var mainGrid = FindMainGrid(grids);
            if (mainGrid == null) return;

            // Obliczenie delty pozycji
            var deltaPosition = newPosition - mainGrid.PositionAndOrientation.Value.Position;

            foreach (var grid in grids)
            {
                UpdateGridPosition(grid, deltaPosition);
                TransferGridOwnership(new[] { grid }, DefaultNewOwner);
                EnableRequiredItemsOnLoad(grid);
            }
        }

        private MyObjectBuilder_CubeGrid FindMainGrid(IEnumerable<MyObjectBuilder_CubeGrid> grids)
        {
            // Możesz dostosować to kryterium w zależności od tego, jak definiujesz "główną siatkę"
            return grids.OrderByDescending(g => g.CubeBlocks.Count).FirstOrDefault();
        }

        private void UpdateGridPosition(MyObjectBuilder_CubeGrid grid, Vector3D deltaPosition)
        {
            // Dodanie delty do aktualnej pozycji siatki
            var newPosition = grid.PositionAndOrientation.Value.Position + deltaPosition;
            grid.PositionAndOrientation = new MyPositionAndOrientation(newPosition, grid.PositionAndOrientation.Value.Forward, grid.PositionAndOrientation.Value.Up);
        }

        private void SpawnEntities(IEnumerable<MyObjectBuilder_CubeGrid> grids)
        {
            foreach (var grid in grids)
            {
                MyAPIGateway.Entities.CreateFromObjectBuilderParallel(grid, false, Increment);
            }
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
                if (grid.Physics != null)
                {
                    grid.Physics.SetSpeeds(Vector3.Zero, Vector3.Zero);
                }
                MyAPIGateway.Entities.AddEntity(grid, true);
            }
        }
    }
}
