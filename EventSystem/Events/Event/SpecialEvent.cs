using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VRageMath;

namespace EventSystem.Event
{
    public class SpecialEvent : EventsBase
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/SpecialEvent");
        private readonly EventSystemConfig _config;

        protected ConcurrentDictionary<long, bool> ParticipatingPlayers { get; } = new ConcurrentDictionary<long, bool>();

        public SpecialEvent(EventSystemConfig config)
        {
            _config = config;
            EventName = "SpecialEvent";
            PrefabStoragePath = Path.Combine("EventSystem", "SpecialEventBP");
        }

        public async override Task ExecuteEvent()
        {
            Log.Info($"Executing SpecialEvent.");

            // Spawn Grid
            string gridName = _config.SpecialEventSettings.PrefabName;
            Vector3D position = new Vector3D(_config.SpecialEventSettings.SpawnPositionX, _config.SpecialEventSettings.SpawnPositionY, _config.SpecialEventSettings.SpawnPositionZ);
            await SpawnGrid(gridName, position);
            //
        }


        public override Task EndEvent()
        {
            // Implementacja logiki końca wydarzenia
            Log.Info($"Ending SpecialEvent.");
            return Task.CompletedTask;
        }

        // Dodaje gracza do listy uczestników eventu.
        public override Task AddPlayer(long steamId)
        {
            ParticipatingPlayers.TryAdd(steamId, true);
            return Task.CompletedTask;
        }

        // Usuwa gracza z listy uczestników eventu.
        public override Task RemovePlayer(long steamId)
        {
            ParticipatingPlayers.TryRemove(steamId, out _);
            return Task.CompletedTask;
        }

        // Sprawdza, czy gracz jest w liście uczestników eventu.
        public override Task<bool> IsPlayerParticipating(long steamId)
        {
            bool isParticipating = ParticipatingPlayers.ContainsKey(steamId);
            return Task.FromResult(isParticipating);
        }

        public override Task CheckPlayerProgress(long steamId)
        {
            return Task.CompletedTask;
        }

        public override Task LoadEventSettings(EventSystemConfig config)
        {
            if (config.SpecialEventSettings == null)
            {
                config.SpecialEventSettings = new SpecialEventConfig
                {
                    IsEnabled = false,
                    ActiveDaysOfMonth = new List<int> { 1, 15, 20 },
                    StartTime = "00:00:00",
                    EndTime = "23:59:59",
                    PrefabName = "MySpecialGrid",
                    SpawnPositionX = 0,
                    SpawnPositionY = 0,
                    SpawnPositionZ = 0,
                };
            }

            var settings = config.SpecialEventSettings;
            IsEnabled = settings.IsEnabled;
            ActiveDaysOfMonth = settings.ActiveDaysOfMonth;
            StartTime = TimeSpan.Parse(settings.StartTime);
            EndTime = TimeSpan.Parse(settings.EndTime);

            // Ustawienie pozycji spawnu na podstawie konfiguracji
            Vector3D spawnPosition = new Vector3D(settings.SpawnPositionX, settings.SpawnPositionY, settings.SpawnPositionZ);

            string activeDaysText = ActiveDaysOfMonth.Count > 0 ? string.Join(", ", ActiveDaysOfMonth) : "Every day";
            LoggerHelper.DebugLog(Log, _config, $"Loaded SpecialEvent settings: IsEnabled={IsEnabled}, Active Days of Month={activeDaysText}, StartTime={StartTime}, EndTime={EndTime}, SpawnPosition={spawnPosition}");

            return Task.CompletedTask;
        }


        public class SpecialEventConfig
        {
            public bool IsEnabled { get; set; }
            public List<int> ActiveDaysOfMonth { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public string PrefabName { get; set; }
            public double SpawnPositionX { get; set; }
            public double SpawnPositionY { get; set; }
            public double SpawnPositionZ { get; set; }
        }

    }
}
