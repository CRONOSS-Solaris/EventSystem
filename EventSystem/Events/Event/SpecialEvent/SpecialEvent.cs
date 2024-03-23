﻿using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

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
            UseEventSpecificConfig = false;
            PrefabStoragePath = Path.Combine("EventSystem", "EventPrefab/Blueprint");
        }

        public override async Task SystemStartEvent()
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"System Start SpecialEvent.");
        }

        public override async Task StartEvent()
        {

        }

        public override async Task SystemEndEvent()
        {
            // Implementacja logiki końca wydarzenia
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Ending SpecialEvent.");
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
                };
            }

            var settings = config.SpecialEventSettings;
            IsEnabled = settings.IsEnabled;
            ActiveDaysOfMonth = settings.ActiveDaysOfMonth;
            StartTime = TimeSpan.Parse(settings.StartTime);
            EndTime = TimeSpan.Parse(settings.EndTime);

            string activeDaysText = ActiveDaysOfMonth.Count > 0 ? string.Join(", ", ActiveDaysOfMonth) : "Every day";
            LoggerHelper.DebugLog(Log, _config, $"Loaded SpecialEvent settings: IsEnabled={IsEnabled}, Active Days of Month={activeDaysText}, StartTime={StartTime}, EndTime={EndTime}");

            return Task.CompletedTask;
        }

        public class SpecialEventConfig
        {
            public bool IsEnabled { get; set; }
            public List<int> ActiveDaysOfMonth { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
        }

    }
}
