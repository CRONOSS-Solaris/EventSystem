//using EventSystem.Events;
//using EventSystem.Utils;
//using NLog;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.IO;
//using System.Threading.Tasks;
//using VRageMath;
//using static EventSystem.Event.SpecialEvent;

//namespace EventSystem.Event
//{
//    // ExampleEvent: A sample event class demonstrating how to implement a custom event.
//    public class ExampleEvent : EventsBase
//    {
//        private readonly EventSystemConfig _config;

//        // ConcurrentDictionary to track participating players (Thread-safe).
//        protected ConcurrentDictionary<long, bool> ParticipatingPlayers { get; } = new ConcurrentDictionary<long, bool>();

//        public ExampleEvent(EventSystemConfig config)
//        {
//            _config = config;
//            EventName = "ExampleEvent";  // Set the name of the event.
//            AllowParticipationInOtherEvents = true;  // Set whether this event allows participation in other events.
//            PrefabStoragePath = Path.Combine("EventSystem", "ExampleEventPrefabs");  // Define the prefab storage path.
//        }

//        // ExecuteEvent: Logic to be executed when the event starts.
//        public async override Task ExecuteEvent()
//        {
//            Log.Info($"Executing {EventName}.");

//            // Example of spawning a grid.
//            string gridName = _config.ExampleEventSettings.PrefabName;
//            Vector3D position = new Vector3D(_config.ExampleEventSettings.SpawnPositionX, _config.ExampleEventSettings.SpawnPositionY, _config.ExampleEventSettings.SpawnPositionZ);
//            await SpawnGrid(gridName, position);
//        }

//        // EndEvent: Logic to be executed when the event ends.
//        public override Task EndEvent()
//        {
//            Log.Info($"Ending {EventName}.");
//            return Task.CompletedTask;
//        }

//        // AddPlayer: Adds a player to the event's participant list.
//        public override Task AddPlayer(long steamId)
//        {
//            ParticipatingPlayers.TryAdd(steamId, true);
//            return Task.CompletedTask;
//        }

//        // RemovePlayer: Removes a player from the event's participant list.
//        public override Task RemovePlayer(long steamId)
//        {
//            ParticipatingPlayers.TryRemove(steamId, out _);
//            return Task.CompletedTask;
//        }

//        // IsPlayerParticipating: Checks if a player is participating in the event.
//        public override Task<bool> IsPlayerParticipating(long steamId)
//        {
//            return Task.FromResult(ParticipatingPlayers.ContainsKey(steamId));
//        }

//        // CheckPlayerProgress: (Optional) Implement to check a player's progress in the event.
//        public override Task CheckPlayerProgress(long steamId)
//        {
//            // Implement specific progress checking logic here.
//            return Task.CompletedTask;
//        }

//        // LoadEventSettings: Load the event settings from the configuration.
//        public override Task LoadEventSettings(EventSystemConfig config)
//        {
//            if (config.ExampleEventConfig == null)
//            {
//                config.ExampleEventConfig = new SpecialEventConfig
//                {
//                    IsEnabled = false,
//                    ActiveDaysOfMonth = new List<int> { 1, 15, 20 },
//                    StartTime = "00:00:00",
//                    EndTime = "23:59:59",
//                    PrefabName = "MySpecialGrid",
//                    SpawnPositionX = 0,
//                    SpawnPositionY = 0,
//                    SpawnPositionZ = 0,
//                };
//            }

//            var settings = config.ExampleEventConfig;
//            IsEnabled = settings.IsEnabled;
//            ActiveDaysOfMonth = settings.ActiveDaysOfMonth;
//            StartTime = TimeSpan.Parse(settings.StartTime);
//            EndTime = TimeSpan.Parse(settings.EndTime);

//            // Ustawienie pozycji spawnu na podstawie konfiguracji
//            Vector3D spawnPosition = new Vector3D(settings.SpawnPositionX, settings.SpawnPositionY, settings.SpawnPositionZ);

//            string activeDaysText = ActiveDaysOfMonth.Count > 0 ? string.Join(", ", ActiveDaysOfMonth) : "Every day";
//            LoggerHelper.DebugLog(Log, _config, $"Loaded ExampleEventConfig settings: IsEnabled={IsEnabled}, Active Days of Month={activeDaysText}, StartTime={StartTime}, EndTime={EndTime}, SpawnPosition={spawnPosition}");

//            return Task.CompletedTask;
//        }

//        // GetParticipantsCount: Returns the count of participants in the event.
//        public override int GetParticipantsCount()
//        {
//            return ParticipatingPlayers.Count;
//        }

//        // ExampleEventConfig: A nested class to hold configuration specific to this event.
//        public class ExampleEventConfig
//        {
//            public bool IsEnabled { get; set; }
//            public List<int> ActiveDaysOfMonth { get; set; }
//            public string StartTime { get; set; }
//            public string EndTime { get; set; }
//            public string PrefabName { get; set; }
//            public double SpawnPositionX { get; set; }
//            public double SpawnPositionY { get; set; }
//            public double SpawnPositionZ { get; set; }
//        }
//    }
//}
