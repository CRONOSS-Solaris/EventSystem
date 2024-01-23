﻿using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        }

        public override Task ExecuteEvent()
        {
            // Implementacja logiki wydarzenia
            Log.Info($"Executing SpecialEvent.");
            return Task.CompletedTask;
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
                    EndTime = "23:59:59"
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
