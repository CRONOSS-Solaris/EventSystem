using EventSystem.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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


        public virtual Task ManageGrid()
        {
            Log.Info("Managing grid. Override this method in derived class.");
            return Task.CompletedTask;
        }

        public virtual async Task CleanupGrids()
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
    }
}
