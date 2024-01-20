using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Generic;

namespace EventSystem.Event
{
    public class SpecialEvent : EventsBase
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly EventSystemConfig _config;

        public SpecialEvent(EventSystemConfig config)
        {
            _config = config;
            Name = "SpecialEvent";
        }

        public override void ExecuteEvent()
        {
            // Tutaj implementacja tego, co ma się dziać podczas eventu
            // Na przykład przyznawanie nagród, zmiana stanu gry, itp.
            Log.Info($"Executing SpecialEvent.");
        }

        public override void LoadEventSettings(EventSystemConfig config)
        {
            // Jeśli SpecialEventSettings nie jest zdefiniowany, ustaw domyślne wartości
            if (config.SpecialEventSettings == null)
            {
                config.SpecialEventSettings = new SpecialEventConfig
                {
                    IsEnabled = false,
                    ActiveDays = new List<DayOfWeek> { DayOfWeek.Friday, DayOfWeek.Saturday },
                    StartTime = "18:00:00",
                    EndTime = "23:59:59"
                };
            }

            var settings = config.SpecialEventSettings;
            IsEnabled = settings.IsEnabled;
            ActiveDays = settings.ActiveDays;

            // Konwersja z string na TimeSpan
            StartTime = TimeSpan.Parse(settings.StartTime);
            EndTime = TimeSpan.Parse(settings.EndTime);

            LoggerHelper.DebugLog(Log, _config, $"Loaded SpecialEvent settings: IsEnabled={IsEnabled}, ActiveDays={string.Join(",", ActiveDays)}, StartTime={StartTime}, EndTime={EndTime}");
        }

        public class SpecialEventConfig
        {
            public bool IsEnabled { get; set; }
            public List<DayOfWeek> ActiveDays { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
        }
    }
}
