using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSystem.Event
{
    public class SpecialTwoEvent : EventsBase
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/SpecialTwoEvent");
        private readonly EventSystemConfig _config;

        public SpecialTwoEvent(EventSystemConfig config)
        {
            _config = config;
            AllowParticipationInOtherEvents = true;
            EventName = "SpecialTwoEvent";
        }

        public override Task ExecuteEvent()
        {
            // Implementacja logiki wydarzenia
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Executing SpecialTwoEvent.");
            return Task.CompletedTask;
        }

        public override Task EndEvent()
        {
            // Implementacja logiki końca wydarzenia
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Ending SpecialTwoEvent.");
            return Task.CompletedTask;
        }

        public override Task CheckPlayerProgress(long steamId)
        {
            return Task.CompletedTask;
        }

        public override Task LoadEventSettings(EventSystemConfig config)
        {
            if (config.SpecialTwoEventSettings == null)
            {
                config.SpecialTwoEventSettings = new SpecialTwoEventConfig
                {
                    IsEnabled = false,
                    ActiveDaysOfMonth = new List<int> { 1, 15, 20 },
                    StartTime = "00:00:00",
                    EndTime = "23:59:59"
                };
            }

            var settings = config.SpecialTwoEventSettings;
            IsEnabled = settings.IsEnabled;
            ActiveDaysOfMonth = settings.ActiveDaysOfMonth;

            StartTime = TimeSpan.Parse(settings.StartTime);
            EndTime = TimeSpan.Parse(settings.EndTime);

            string activeDaysText = ActiveDaysOfMonth.Count > 0 ? string.Join(", ", ActiveDaysOfMonth) : "Every day";
            LoggerHelper.DebugLog(Log, _config, $"Loaded SpecialTwoEvent settings: IsEnabled={IsEnabled}, Active Days of Month={activeDaysText}, StartTime={StartTime}, EndTime={EndTime}");

            return Task.CompletedTask;
        }

        public class SpecialTwoEventConfig
        {
            public bool IsEnabled { get; set; }
            public List<int> ActiveDaysOfMonth { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
        }
    }
}
