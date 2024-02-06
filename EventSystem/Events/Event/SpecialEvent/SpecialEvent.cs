using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using System;
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

        public SpecialEvent(EventSystemConfig config)
        {
            _config = config;
            EventName = "SpecialEvent";
            AllowParticipationInOtherEvents = false;
            PrefabStoragePath = Path.Combine("EventSystem", "SpecialEventBP");
            UseEventSpecificConfig = false;
        }

        public override async Task SystemStartEvent()
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"System Start SpecialEvent.");
            await SpawnArena();
        }

        public override async Task StartEvent()
        {

        }

        private async Task SpawnArena()
        {
            string gridName = _config.SpecialEventSettings.PrefabName;
            Vector3D position = new Vector3D(
                _config.SpecialEventSettings.SpawnPositionX,
                _config.SpecialEventSettings.SpawnPositionY,
                _config.SpecialEventSettings.SpawnPositionZ);

            var spawnedEntityIds = await SpawnGrid(gridName, position);
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Spawned grid '{gridName}' with entity IDs: {string.Join(", ", spawnedEntityIds)}");
        }

        public override async Task SystemEndEvent()
        {
            // Implementacja logiki końca wydarzenia
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Ending SpecialEvent.");
            await CleanupGrids();
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
                    PrefabName = "arenapvp",
                    SpawnPositionX = -42596.88,
                    SpawnPositionY = 40764.17,
                    SpawnPositionZ = -16674.06,
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
