using EventSystem.Utils;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using VRageMath;

namespace EventSystem.Events
{
    public abstract partial class EventsBase
    {
        //list of grids created by event
        protected ConcurrentDictionary<long, bool> SpawnedGridsEntityIds { get; } = new ConcurrentDictionary<long, bool>();

        // Methods to manage additional event elements, e.g. grids, objects.
        public virtual async Task<HashSet<long>> SpawnGrid(string gridName, Vector3D position)
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Attempting to spawn grid '{gridName}' at position {position}.");

            var prefabFolderPath = Path.Combine(EventSystemMain.Instance.StoragePath, PrefabStoragePath);
            var filePath = Path.Combine(prefabFolderPath, $"{gridName}.sbc");

            if (!File.Exists(filePath))
            {
                Log.Error($"File not found: {filePath}. Unable to spawn grid '{gridName}'.");
                return new HashSet<long>();
            }

            try
            {
                HashSet<long> entityIds = await GridSerializer.LoadAndSpawnGrid(prefabFolderPath, gridName, position);

                if (entityIds != null && entityIds.Count > 0)
                {
                    foreach (var entityId in entityIds)
                    {
                        SpawnedGridsEntityIds.TryAdd(entityId, true);
                    }
                    LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Successfully spawned grid '{gridName}' with entity IDs: {string.Join(", ", entityIds)}.");
                    return entityIds;
                }
                else
                {
                    Log.Warn($"Grid '{gridName}' was not spawned. No entities were created.");
                    return new HashSet<long>();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"An error occurred while attempting to spawn grid '{gridName}': {ex.Message}");
                return new HashSet<long>();
            }
        }

        //Grid management
        public virtual Task ManageGrid()
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Managing grid. Override this method in derived class.");
            return Task.CompletedTask;
        }

        // Cleaning the grids of the world
        protected virtual async Task CleanupGrids()
        {
            var removalTasks = new List<Task>();

            foreach (var keyValuePair in SpawnedGridsEntityIds)
            {
                long entityId = keyValuePair.Key;
                removalTasks.Add(RemoveEntityAsync(entityId));
            }

            await Task.WhenAll(removalTasks);
            SpawnedGridsEntityIds.Clear();
        }

        // Method to search for a block by custom name in grids created by an event.
        protected async Task<Vector3D?> FindBlockPositionByName(string blockName)
        {
            Vector3D? foundPosition = null;

            // Searching each grid created by the event.
            foreach (var entityId in SpawnedGridsEntityIds.Keys)
            {
                var grid = MyAPIGateway.Entities.GetEntityById(entityId) as IMyCubeGrid;
                if (grid != null)
                {
                    // Search the blocks in the grid for a block with the appropriate custom name.
                    var blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, b => b.FatBlock != null && (b.FatBlock as IMyTerminalBlock)?.CustomName.Contains(blockName) == true);

                    var block = blocks.FirstOrDefault()?.FatBlock as IMyTerminalBlock;
                    if (block != null)
                    {
                        // If a block is found, return its position.
                        foundPosition = block.GetPosition();
                        break; // Break the loop if a block is found.
                    }
                }
            }

            return await Task.FromResult(foundPosition);
        }


    }
}
