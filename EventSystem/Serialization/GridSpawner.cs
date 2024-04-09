using EventSystem.Utils;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using Sandbox.Game.World.Generator;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;

namespace EventSystem.Serialization
{

    //Using QuantumHangar by Casimir, I was inspired and used some valuable methods, which significantly accelerated the writing of this class.

    public class GridSpawner
    {
        public readonly Logger Log = LogManager.GetLogger("EventSystem/GridSpawner");
        private HashSet<MyCubeGrid> _spawnedGrids = new HashSet<MyCubeGrid>();

        //Bounds
        private BoundingSphereD _sphereD;

        //Delta
        private Vector3D _delta3D;

        public GridSpawner()
        {
        }

        public Task<HashSet<long>> SpawnGrids(IEnumerable<MyObjectBuilder_CubeGrid> grids, Vector3D position, GridSpawnSettings settings)
        {
            var tcs = new TaskCompletionSource<HashSet<long>>();
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"(SpawnGrids) Attempting to spawn grids. Initial position: {position}");

            MyAPIGateway.Utilities.InvokeOnGameThread(async () =>
            {
                try
                {
                    position = (Vector3D)FindPastePosition(position);
                    if (position == Vector3D.Zero)
                    {
                        LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "(SpawnGrids) Unable to find spawn position. Position is Vector3D.Zero.");
                        tcs.SetResult(new HashSet<long>());
                        return;
                    }

                    LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"(SpawnGrids) Found spawn position: {position}");

                    var currentSpawnedGridsEntityIds = new HashSet<long>();
                    var success = await ProcessGrids(grids, position, currentSpawnedGridsEntityIds, settings);

                    if (success)
                    {
                        LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"(SpawnGrids) Successfully spawned grids. Entity IDs: {string.Join(", ", currentSpawnedGridsEntityIds)}");
                        tcs.SetResult(currentSpawnedGridsEntityIds);
                    }
                    else
                    {
                        LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "(SpawnGrids) Failed to spawn grids.");
                        tcs.SetResult(new HashSet<long>());
                    }
                }
                catch (Exception ex)
                {
                    LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"(SpawnGrids) Exception occurred: {ex.Message}");
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        private async Task<bool> ProcessGrids(IEnumerable<MyObjectBuilder_CubeGrid> grids, Vector3D newPosition, HashSet<long> currentSpawnedGridsEntityIds, GridSpawnSettings settings)
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "(ProcessGrids) Processing grids for spawning.");

            // Remap to fix entity conflicts
            MyEntities.RemapObjectBuilderCollection(grids);

            var mainGrid = FindMainGrid(grids); // Find the main grid
            if (mainGrid == null)
            {
                Log.Warn("(ProcessGrids) Main grid not found.");
                return false;
            }

            bool isMainGridStatic = mainGrid.IsStatic;
            Log.Debug($"(ProcessGrids) Main grid static status: {isMainGridStatic}");

            _delta3D = _sphereD.Center - mainGrid.PositionAndOrientation.Value.Position;
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"(ProcessGrids) Delta calculated for grid positioning: {_delta3D}");

            bool spawnSuccess = true;

            foreach (var grid in grids)
            {
                UpdateGridPosition(grid, _delta3D, newPosition);
                TransferGridOwnership(new[] { grid }, settings);
                EnableRequiredItemsOnLoad(grid, settings);
            }

            try
            {
                await SpawnEntities(grids, isMainGridStatic, currentSpawnedGridsEntityIds);
            }
            catch (Exception ex)
            {
                Log.Error($"(ProcessGrids) Error spawning entities: {ex.Message}");
                spawnSuccess = false;
            }

            return spawnSuccess;
        }


        private MyObjectBuilder_CubeGrid FindMainGrid(IEnumerable<MyObjectBuilder_CubeGrid> grids)
        {
            return grids.OrderByDescending(g => g.CubeBlocks.Count).FirstOrDefault();
        }

        private void UpdateGridPosition(MyObjectBuilder_CubeGrid grid, Vector3D deltaPosition, Vector3D targetPos)
        {
            // Log the initial position of the grid
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"(UpdateGridPosition) Initial grid position: X={targetPos.X}, Y={targetPos.Y}, Z={targetPos.Z}");

            // Log the delta being applied to the grid position
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"(UpdateGridPosition) Applying delta: X={deltaPosition.X}, Y={deltaPosition.Y}, Z={deltaPosition.Z}");

            // Update grid position by delta
            var newPosition = targetPos - deltaPosition;

            //Now need to create a delta change from the initial position to the target position
            var delta = targetPos - grid.PositionAndOrientation.Value.Position;

            // Log the new position of the grid after applying delta
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"(UpdateGridPosition) New grid position: X={newPosition.X}, Y={newPosition.Y}, Z={newPosition.Z}");

            Vector3D currentPos = grid.PositionAndOrientation.Value.Position;
            //MatrixD worldMatrix = MatrixD.CreateWorld(CurrentPos + Delta, grid.PositionAndOrientation.Value.Orientation.Forward, grid.PositionAndOrientation.Value.Orientation.Up,);
            grid.PositionAndOrientation = new MyPositionAndOrientation(currentPos + delta,
                grid.PositionAndOrientation.Value.Orientation.Forward,
                grid.PositionAndOrientation.Value.Orientation.Up);
        }


        private async Task SpawnEntities(IEnumerable<MyObjectBuilder_CubeGrid> grids, bool makeStatic, HashSet<long> currentSpawnedGridsEntityIds)
        {
            var tasks = new List<Task>();

            foreach (var gridBuilder in grids)
            {
                if (makeStatic)
                {
                    gridBuilder.IsStatic = true;
                }

                var tcs = new TaskCompletionSource<bool>();
                Action<IMyEntity> completionCallback = (spawnedEntity) =>
                {
                    Increment(spawnedEntity, currentSpawnedGridsEntityIds);
                    tcs.SetResult(true);
                };

                MyAPIGateway.Entities.CreateFromObjectBuilderParallel(gridBuilder, true, completionCallback);
                tasks.Add(tcs.Task);
            }

            await Task.WhenAll(tasks);
        }

        private Vector3D? FindPastePosition(Vector3D target)
        {
            MyGravityProviderSystem.CalculateNaturalGravityInPoint(target, out var val);
            if (val == 0)
                //following method is what SEworldgen uses. We only really need to use this in space
                return FindSuitableJumpLocationSpace(target);



            MyPlanet planet = MyGamePruningStructure.GetClosestPlanet(target);
            Vector3D closestSurfacePoint = planet.GetClosestSurfacePointGlobal(target);
            Vector3D planetCenter = planet.PositionComp.GetPosition();
            double Targetdistance = Vector3D.Distance(target, planetCenter);
            double lowestPointDistance = Vector3D.Distance(closestSurfacePoint, planetCenter);

            Log.Warn($"T{Targetdistance} L{lowestPointDistance}");
            if (Targetdistance < lowestPointDistance || Targetdistance - lowestPointDistance < 350)
            {
                return MyEntities.FindFreePlaceCustom(closestSurfacePoint, (float)_sphereD.Radius, 125, 15, 1.5f, 2.5f);
            }


            return MyEntities.FindFreePlaceCustom(target, (float)_sphereD.Radius, 125, 15, 1.5f, 5);
        }

        private Vector3D? FindSuitableJumpLocationSpace(Vector3D desiredLocation)
        {
            var mObjectsInRange = new List<MyObjectSeed>();
            var mEntitiesInRange = new List<MyEntity>();


            var inflated = _sphereD;
            inflated.Radius *= 1.5;
            inflated.Center = desiredLocation;

            var vector3D = desiredLocation;

            MyProceduralWorldGenerator.Static.OverlapAllAsteroidSeedsInSphere(inflated, mObjectsInRange);
            var mObstaclesInRange = mObjectsInRange.Select(item3 => item3.BoundingVolume).ToList();
            mObjectsInRange.Clear();

            MyProceduralWorldGenerator.Static.GetAllInSphere<MyStationCellGenerator>(inflated, mObjectsInRange);
            mObstaclesInRange.AddRange(mObjectsInRange.Select(item4 => item4.UserData).OfType<MyStation>().Select(myStation => new BoundingBoxD(myStation.Position - MyStation.SAFEZONE_SIZE, myStation.Position + MyStation.SAFEZONE_SIZE)).Where(item => item.Contains(vector3D) != 0));

            mObjectsInRange.Clear();


            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref inflated, mEntitiesInRange);
            mObstaclesInRange.AddRange(from item5 in mEntitiesInRange where !(item5 is MyPlanet) select item5.PositionComp.WorldAABB.GetInflated(inflated.Radius));

            const int num = 10;
            var num2 = 0;
            BoundingBoxD? boundingBoxD = null;
            var flag2 = false;
            while (num2 < num)
            {
                num2++;
                var flag = false;
                foreach (var item6 in mObstaclesInRange
                             .Select(item6 => new { item6, containmentType = item6.Contains(vector3D) })
                             .Where(@t =>
                                 @t.containmentType == ContainmentType.Contains ||
                                 @t.containmentType == ContainmentType.Intersects)
                             .Select(@t => @t.item6))
                {
                    boundingBoxD ??= item6;
                    boundingBoxD = boundingBoxD.Value.Include(item6);
                    boundingBoxD = boundingBoxD.Value.Inflate(1.0);
                    vector3D = ClosestPointOnBounds(boundingBoxD.Value, vector3D);
                    flag = true;
                    break;
                }

                if (flag) continue;
                flag2 = true;
                break;
            }

            mObstaclesInRange.Clear();
            mEntitiesInRange.Clear();
            mObjectsInRange.Clear();
            if (flag2) return vector3D;
            return null;
        }

        private static Vector3D ClosestPointOnBounds(BoundingBoxD b, Vector3D p)
        {
            var vector3D = (p - b.Center) / b.HalfExtents;
            switch (vector3D.AbsMaxComponent())
            {
                case 0:
                    p.X = vector3D.X > 0.0 ? b.Max.X : b.Min.X;
                    break;
                case 1:
                    p.Y = vector3D.Y > 0.0 ? b.Max.Y : b.Min.Y;
                    break;
                case 2:
                    p.Z = vector3D.Z > 0.0 ? b.Max.Z : b.Min.Z;
                    break;
            }

            return p;
        }

        private void TransferGridOwnership(IEnumerable<MyObjectBuilder_CubeGrid> grids, GridSpawnSettings settings)
        {
            foreach (var grid in grids)
            {
                foreach (var block in grid.CubeBlocks)
                {
                    block.Owner = settings.GridOwnerSettings.OwnerGrid;
                    block.BuiltBy = settings.GridOwnerSettings.OwnerGrid;
                }
            }
        }

        private void EnableRequiredItemsOnLoad(MyObjectBuilder_CubeGrid grid, GridSpawnSettings settings)
        {
            foreach (var block in grid.CubeBlocks)
            {
                if (block is MyObjectBuilder_FunctionalBlock functionalBlock)
                {
                    functionalBlock.Enabled = settings.FunctionalBlockSettings.Enabled;
                }
            }

            if (grid is MyObjectBuilder_CubeGrid gridBlock)
            {
                gridBlock.DampenersEnabled = settings.CubeGridSettings.DampenersEnabled;
                gridBlock.Editable = settings.CubeGridSettings.Editable;
                gridBlock.IsPowered = settings.CubeGridSettings.IsPowered;
                gridBlock.IsStatic = settings.CubeGridSettings.IsStatic;
                gridBlock.DestructibleBlocks = settings.CubeGridSettings.DestructibleBlocks;
            }
        }

        public void Increment(IMyEntity entity, HashSet<long> currentSpawnedGridsEntityIds)
        {
            var grid = entity as MyCubeGrid;
            if (grid != null)
            {
                _spawnedGrids.Add(grid);
                currentSpawnedGridsEntityIds.Add(grid.EntityId);
                LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Grid spawned: EntityId {grid.EntityId}");

                if (grid.Physics != null)
                {
                    grid.Physics.SetSpeeds(Vector3.Zero, Vector3.Zero);
                }
                MyAPIGateway.Entities.AddEntity(grid, true);
            }
        }
    }
}
