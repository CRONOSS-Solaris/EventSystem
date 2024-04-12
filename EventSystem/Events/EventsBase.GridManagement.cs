using EventSystem.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VRage;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components;
using VRageMath;

namespace EventSystem.Events
{
    public abstract partial class EventsBase
    {
        //list of grids created by event
        protected ConcurrentDictionary<long, bool> SpawnedGridsEntityIds { get; } = new ConcurrentDictionary<long, bool>();
        public static ConcurrentDictionary<string, GridSpawnSettings> GridSettingsDictionary = new ConcurrentDictionary<string, GridSpawnSettings>();
        protected ConcurrentDictionary<long, bool> safezoneEntityIds = new ConcurrentDictionary<long, bool>();


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
                // Pobranie ustawień dla siatki
                GridSpawnSettings settings;
                if (!GridSettingsDictionary.TryGetValue(gridName, out settings))
                {
                    settings = new GridSpawnSettings();
                }

                HashSet<long> entityIds = await GridSerializer.LoadAndSpawnGrid(prefabFolderPath, gridName, position, settings);

                if (entityIds != null && entityIds.Count > 0)
                {
                    foreach (var entityId in entityIds)
                    {
                        SpawnedGridsEntityIds.TryAdd(entityId, true);
                    }
                    SaveEntityIds();
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
            SaveEntityIds();
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

        /// <summary>
        /// Calculates the geometric center of all spawned grids specified by their entity IDs.
        /// </summary>
        /// <param name="spawnedEntityIds">A set of entity IDs for which the center is to be calculated.</param>
        /// <returns>The calculated center as a Vector3D. Returns Vector3D.Zero if no entities are found or input set is empty.</returns>
        public Vector3D CalculateGridCenter(HashSet<long> spawnedEntityIds)
        {
            Vector3D center = Vector3D.Zero;
            int count = 0;

            foreach (var entityId in spawnedEntityIds)
            {
                if (MyAPIGateway.Entities.TryGetEntityById(entityId, out var entity))
                {
                    center += entity.GetPosition();
                    count++;
                }
            }

            if (count > 0)
            {
                center /= count;
            }

            return center;
        }

        /// <summary>
        /// Creates a safe zone with customizable settings around a specified center point. This method can be used
        /// to dynamically generate safe zones during events, offering protection or restrictions in designated areas.
        /// </summary>
        /// <param name="center">The center point of the safe zone.</param>
        /// <param name="radius">The radius of the safe zone if it's a sphere, or half the edge length if it's a cube.</param>
        /// <param name="shape">The shape of the safe zone, either Sphere or Cube.</param>
        /// <param name="isEnabled">Determines whether the safe zone is enabled upon creation. Default is true.</param>
        /// <param name="accessTypePlayers">Defines the access type for players. Default is Blacklist.</param>
        /// <param name="accessTypeFactions">Defines the access type for factions. Default is Blacklist.</param>
        /// <param name="accessTypeGrids">Defines the access type for grids. Default is Whitelist.</param>
        /// <param name="accessTypeFloatingObjects">Defines the access type for floating objects. Default is Blacklist.</param>
        /// <param name="allowedActions">Specifies the actions that are allowed within the safe zone. Default is All.</param>
        /// <param name="modelColor">The color of the safe zone model. Pass null to use the default red color.</param>
        /// <param name="texture">The texture of the safe zone. Default is "SafeZone_Texture_Restricted".</param>
        /// <param name="isVisible">Determines whether the safe zone is visible. Default is true.</param>
        /// <param name="displayName">The display name of the safe zone. If empty or null, a generic name will be used.</param>
        /// Example:
        /// ZoneShape shape = _config.WarZoneGridSettings.Shape == EventsBase.ZoneShape.Sphere ? ZoneShape.Sphere : ZoneShape.Cube;
        /// CreateSafeZone(sphereCenter, Radius, shape, true, MySafeZoneAccess.Blacklist, MySafeZoneAccess.Blacklist, MySafeZoneAccess.Whitelist, MySafeZoneAccess.Blacklist, MySafeZoneAction.Damage | MySafeZoneAction.Shooting, new SerializableVector3(0f, 0f, 1f), "SafeZone_Texture_Restricted", true, $"{EventName}SafeZone");
        protected void CreateSafeZone(Vector3D center, double radius, ZoneShape shape, bool isEnabled = true, MySafeZoneAccess accessTypePlayers = MySafeZoneAccess.Blacklist, MySafeZoneAccess accessTypeFactions = MySafeZoneAccess.Blacklist, MySafeZoneAccess accessTypeGrids = MySafeZoneAccess.Whitelist, MySafeZoneAccess accessTypeFloatingObjects = MySafeZoneAccess.Blacklist, MySafeZoneAction allowedActions = MySafeZoneAction.All, SerializableVector3? modelColor = null, string texture = "SafeZone_Texture_Default", bool isVisible = true, string displayName = "")
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                try
                {
                    SerializableVector3 color = modelColor ?? new SerializableVector3(0f, 0f, 0f);

                    var safezoneDefinition = new MyObjectBuilder_SafeZone
                    {
                        PositionAndOrientation = new MyPositionAndOrientation(center, Vector3.Forward, Vector3.Up),
                        Enabled = isEnabled,
                        AccessTypePlayers = accessTypePlayers,
                        AccessTypeFactions = accessTypeFactions,
                        AccessTypeGrids = accessTypeGrids,
                        AccessTypeFloatingObjects = accessTypeFloatingObjects,
                        AllowedActions = allowedActions,
                        ModelColor = color,
                        Texture = texture,
                        IsVisible = isVisible,
                        DisplayName = displayName
                    };

                    // Ustawienie rozmiaru strefy
                    if (shape == ZoneShape.Sphere)
                    {
                        safezoneDefinition.Shape = MySafeZoneShape.Sphere;
                        safezoneDefinition.Radius = (float)radius;
                    }
                    else // Dla boxa
                    {
                        safezoneDefinition.Shape = MySafeZoneShape.Box;
                        safezoneDefinition.Size = new Vector3((float)radius, (float)radius, (float)radius);
                    }

                    var safezoneEntity = MyAPIGateway.Entities.CreateFromObjectBuilder(safezoneDefinition);
                    if (safezoneEntity != null)
                    {
                        MyAPIGateway.Entities.AddEntity(safezoneEntity, true);
                        safezoneEntityIds.TryAdd(safezoneEntity.EntityId, true);
                        SaveEntityIds();
                    }
                    else
                    {
                        Log.Error("Failed to create safezone. Entity creation returned null.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception occurred while creating safezone: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Removes all safe zones previously created by this event. This method should be called
        /// when the event ends or when the safe zones are no longer needed, to clean up all
        /// safe zone entities and release resources.
        /// </summary>
        protected void RemoveSafeZone()
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                var safezoneIds = safezoneEntityIds.Keys.ToList();

                foreach (var safezoneId in safezoneIds)
                {
                    if (MyAPIGateway.Entities.TryGetEntityById(safezoneId, out var safezoneEntity))
                    {
                        safezoneEntity.Close();
                        MyAPIGateway.Entities.RemoveEntity(safezoneEntity);

                        safezoneEntityIds.TryRemove(safezoneId, out _);
                    }
                }
                SaveEntityIds();
            });
        }
    }
}
