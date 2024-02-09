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

        /// <summary>
        /// Spawns a grid in the world at the specified position.
        /// </summary>
        /// <param name="gridName">The name of the grid to spawn.</param>
        /// <param name="position">The position where the grid should be spawned.</param>
        /// <returns>A HashSet of entity IDs representing the spawned grid.</returns>
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

        /// <summary>
        /// Manages additional event elements, such as grids and objects.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual Task ManageGrid()
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Managing grid. Override this method in derived class.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Cleans up the grids created by the event.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Finds the position of a block in the spawned grids by its custom name.
        /// </summary>
        /// <param name="blockName">The custom name of the block to search for.</param>
        /// <returns>The position of the found block, or null if not found.</returns>
        public Vector3D? FindBlockPositionByName(string blockNameSubstring)
        {
            Vector3D? foundPosition = null;

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Start searching for block containing '{blockNameSubstring}' in name.");

            foreach (var entityId in SpawnedGridsEntityIds.Keys)
            {
                var grid = MyAPIGateway.Entities.GetEntityById(entityId) as IMyCubeGrid;
                if (grid != null)
                {
                    var blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, b => b.FatBlock != null && (b.FatBlock as IMyTerminalBlock)?.CustomName.Contains(blockNameSubstring) == true);

                    var block = blocks.FirstOrDefault()?.FatBlock as IMyTerminalBlock;
                    if (block != null)
                    {
                        foundPosition = block.GetPosition();
                        break;
                    }
                }
            }

            if (!foundPosition.HasValue)
            {
                Log.Warn($"Could not find any block containing '{blockNameSubstring}'.");
            }

            return foundPosition;
        }


    }
}
