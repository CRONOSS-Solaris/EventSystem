using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Generic;
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
        }

        public override Task ExecuteEvent()
        {
            // Tutaj implementacja tego, co ma się dziać podczas eventu
            // Na przykład przyznawanie nagród, zmiana stanu gry, itp.
            Log.Info($"Executing SpecialEvent.");
            return Task.CompletedTask; // Zwraca zakończone zadanie
        }

        public override Task EndEvent()
        {
            // Implementacja logiki końca wydarzenia
            Log.Info($"Ending SpecialEvent.");
            return Task.CompletedTask; // Zwraca zakończone zadanie
        }

        public override Task LoadEventSettings(EventSystemConfig config)
        {
            if (config.SpecialEventSettings == null)
            {
                config.SpecialEventSettings = new SpecialEventConfig
                {
                    IsEnabled = false,
                    ActiveDays = new List<DayOfWeek> { DayOfWeek.Friday, DayOfWeek.Saturday },
                    StartTime = "00:00:00",
                    EndTime = "23:59:59"
                };
            }

            var settings = config.SpecialEventSettings;
            IsEnabled = settings.IsEnabled;
            ActiveDays = settings.ActiveDays;

            StartTime = TimeSpan.Parse(settings.StartTime);
            EndTime = TimeSpan.Parse(settings.EndTime);

            string activeDaysText = ActiveDays.Count > 0 ? string.Join(",", ActiveDays) : "Everyday";
            LoggerHelper.DebugLog(Log, _config, $"Loaded SpecialEvent settings: IsEnabled={IsEnabled}, ActiveDays={activeDaysText}, StartTime={StartTime}, EndTime={EndTime}");
            
            return Task.CompletedTask; // Zwraca zakończone zadanie
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
