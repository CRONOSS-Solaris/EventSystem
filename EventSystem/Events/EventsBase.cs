using EventSystem.Utils;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VRageMath;

namespace EventSystem.Events
{
    public abstract class EventsBase
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/EventsBase");
        
        //location of the profile with bluepirnt grids
        protected virtual string PrefabStoragePath { get; set; }
        
        //list of players who joined the event
        protected ConcurrentDictionary<long, bool> ParticipatingPlayers { get; } = new ConcurrentDictionary<long, bool>();

        //list of grids created by event
        protected ConcurrentDictionary<long, bool> SpawnedGridsEntityIds { get; } = new ConcurrentDictionary<long, bool>();


        //Determines whether an event requires that a player not be in another event to join it 
        public bool AllowParticipationInOtherEvents { get; set; }

        // The name of the event, it can be set in derived classes.
        public string EventName { get; set; }

        // Determines whether the event is enabled.
        public bool IsEnabled { get; set; }

        // List of days of the month when the event is active.
        public List<int> ActiveDaysOfMonth { get; set; }

        // Event start time.
        public TimeSpan StartTime { get; set; }

        // End time of the event.
        public TimeSpan EndTime { get; set; }

        // Method to perform specific actions related to an event.
        public abstract Task ExecuteEvent();

        // Method to implement activities related to the end of the event.
        public abstract Task EndEvent();

        // Method to load the settings of a specific event from the configuration.
        public abstract Task LoadEventSettings(EventSystemConfig config);

        // Adds the player to the list of event participants.
        public abstract Task AddPlayer(long steamId);

        // Removes the player from the list of event participants.
        public abstract Task RemovePlayer(long steamId);

        // Checks if the player is in the list of event participants.
        public abstract Task<bool> IsPlayerParticipating(long steamId);

        // Checks the player's progress in the event.
        public abstract Task CheckPlayerProgress(long steamId);

        // Checks if the event is active at the moment.
        public bool IsActiveNow()
        {
            var now = DateTime.Now;
            bool isActiveToday = ActiveDaysOfMonth.Count == 0 || ActiveDaysOfMonth.Contains(now.Day);
            bool isActiveTime = now.TimeOfDay >= StartTime && now.TimeOfDay <= EndTime;
            bool isActive = IsEnabled && isActiveToday && isActiveTime;

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Checking if '{EventName}' is active now:");
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Current time: {now}");
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"IsEnabled: {IsEnabled}");
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Active today ({now.Day}): {isActiveToday}");
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Active time ({now.TimeOfDay}): {isActiveTime}");
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Is active: {isActive}");

            return isActive;
        }


        // Calculates the time left until the event starts.
        public TimeSpan GetNextStartTime(DateTime now)
        {
            var startOfDay = now.Date.Add(StartTime);
            return now < startOfDay ? startOfDay - now : TimeSpan.Zero;
        }

        // Calculates the time remaining in the event.
        public TimeSpan GetNextEndTime(DateTime now)
        {
            var endOfDay = now.Date.Add(EndTime);
            return now < endOfDay ? endOfDay - now : TimeSpan.Zero;
        }

        // Calculates the time remaining in the event.
        public virtual async Task AwardPlayer(long steamId, long points)
        {
            // Get the database manager from the main plugin
            var databaseManager = EventSystemMain.Instance.DatabaseManager;

            // Get the configuration
            var config = EventSystemMain.Instance.Config;

            if (config.UseDatabase)
            {
                // Reward logic in the database
                await databaseManager.UpdatePlayerPointsAsync(steamId.ToString(), points);
            }
            else
            {
                // Get the player account manager from the main plugin
                var xmlManager = EventSystemMain.Instance.PlayerAccountXmlManager;
                bool updateResult = await xmlManager.UpdatePlayerPointsAsync(steamId, points).ConfigureAwait(false);
                if (!updateResult)
                {
                    // Handling the situation when the player account does not exist (if necessary).
                }
            }
        }

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


        private Task RemoveEntityAsync(long gridId)
        {
            var tcs = new TaskCompletionSource<bool>();

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                try
                {
                    var entity = MyAPIGateway.Entities.GetEntityById(gridId);
                    var grid = entity as MyCubeGrid;
                    if (grid != null)
                    {
                        grid.Close();
                        MyAPIGateway.Entities.RemoveEntity(entity);
                        Log.Info($"Grid with EntityId: {gridId} closed and removed successfully.");
                        tcs.SetResult(true);
                    }
                    else
                    {
                        Log.Warn($"Grid with EntityId: {gridId} not found.");
                        tcs.SetResult(false);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error removing grid.");
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }


        // Checks if the event is active on the specified day of the month.
        public bool IsActiveOnDayOfMonth(int day)
        {
            // Returns true if the event is active on the specified day of the month
            return ActiveDaysOfMonth.Count == 0 || ActiveDaysOfMonth.Contains(day);
        }

        public virtual int GetParticipantsCount()
        {
            return 0;
        }
    }
}
