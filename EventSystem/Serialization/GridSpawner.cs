﻿using EventSystem.Utils;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World.Generator;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using System;

namespace EventSystem.Serialization
{
    public class GridSpawner
    {
        public readonly Logger Log = LogManager.GetLogger("EventSystem/GridSpawner");
        private long DefaultNewOwner = 144115188075855881;
        private HashSet<MyCubeGrid> _spawnedGrids = new HashSet<MyCubeGrid>();

        //Bounds
        private BoundingSphereD _sphereD;

        //Delta
        private Vector3D _delta3D;

        public GridSpawner()
        {
        }

        public async Task<bool> SpawnGrids(IEnumerable<MyObjectBuilder_CubeGrid> grids, Vector3D position)
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Starting to spawn grids at {position}");

            position = (Vector3D)FindPastePosition(position);
            if (position == Vector3D.Zero)
            {
                LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Spawn position is Vector3D.Zero, returning false.");
                return false;
            }


            bool gridsSpawned = await GameEvents.InvokeActionAsync(() => ProcessGrids(grids, position));

            // Logowanie sukcesu lub niepowodzenia procesu spawnowania wewnątrz ProcessGrids
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Grid spawn process completed. Success: {gridsSpawned}");
            return gridsSpawned;
        }

        private bool ProcessGrids(IEnumerable<MyObjectBuilder_CubeGrid> grids, Vector3D newPosition)
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Processing grids for spawning.");

            var mainGrid = FindMainGrid(grids); // Find the main grid
            if (mainGrid == null)
            {
                Log.Warn("Main grid not found.");
                return false;
            }

            bool isMainGridStatic = mainGrid.IsStatic;
            Log.Debug($"Main grid static status: {isMainGridStatic}");

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"_sphereD.Center: X={_sphereD.Center.X}, Y={_sphereD.Center.Y}, Z={_sphereD.Center.Z}");

            _delta3D = _sphereD.Center - mainGrid.PositionAndOrientation.Value.Position;
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Delta calculated for grid positioning: {_delta3D}");

            bool spawnSuccess = true;

            // Apply delta to each grid (main and sub-grids)
            foreach (var grid in grids)
            {
                UpdateGridPosition(grid, _delta3D);
                TransferGridOwnership(new[] { grid }, DefaultNewOwner);
                EnableRequiredItemsOnLoad(grid);
            }

            try
            {
                SpawnEntities(grids, isMainGridStatic);
            }
            catch (Exception ex)
            {
                Log.Error($"Error spawning entities: {ex.Message}");
                spawnSuccess = false;
            }

            if (!spawnSuccess)
            {
                LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Error occurred during grid spawning.");
                return false;
            }

            
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Grids processing completed successfully.");
            return true;
        }


        private MyObjectBuilder_CubeGrid FindMainGrid(IEnumerable<MyObjectBuilder_CubeGrid> grids)
        {
            return grids.OrderByDescending(g => g.CubeBlocks.Count).FirstOrDefault();
        }

        private void UpdateGridPosition(MyObjectBuilder_CubeGrid grid, Vector3D deltaPosition)
        {
            // Log the initial position of the grid
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Initial grid position: X={grid.PositionAndOrientation.Value.Position.X}, Y={grid.PositionAndOrientation.Value.Position.Y}, Z={grid.PositionAndOrientation.Value.Position.Z}");

            // Log the delta being applied to the grid position
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Applying delta: X={deltaPosition.X}, Y={deltaPosition.Y}, Z={deltaPosition.Z}");

            // Update grid position by delta
            var newPosition = grid.PositionAndOrientation.Value.Position + deltaPosition;

            // Log the new position of the grid after applying delta
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"New grid position: X={newPosition.X}, Y={newPosition.Y}, Z={newPosition.Z}");

            grid.PositionAndOrientation = new MyPositionAndOrientation(newPosition, grid.PositionAndOrientation.Value.Forward, grid.PositionAndOrientation.Value.Up);
        }


        private void SpawnEntities(IEnumerable<MyObjectBuilder_CubeGrid> grids, bool makeStatic)
        {
            foreach (var gridBuilder in grids)
            {
                
                if (makeStatic)
                {
                    gridBuilder.IsStatic = true;
                }

                MyAPIGateway.Entities.CreateFromObjectBuilderParallel(gridBuilder, false, Increment);
            }
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
