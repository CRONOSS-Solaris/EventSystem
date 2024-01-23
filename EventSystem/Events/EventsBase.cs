using EventSystem.Utils;
using NLog;
using Sandbox.Engine.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSystem.Events
{
    public abstract class EventsBase
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/EventsBase");

        // Nazwa eventu, można ją ustawić w klasach pochodnych.
        public string EventName { get; set; }

        // Określa, czy event jest włączony.
        public bool IsEnabled { get; set; }

        // Lista dni miesiąca, w których event jest aktywny.
        public List<int> ActiveDaysOfMonth { get; set; }

        // Godzina rozpoczęcia eventu.
        public TimeSpan StartTime { get; set; }

        // Godzina zakończenia eventu.
        public TimeSpan EndTime { get; set; }

        // Metoda do wykonania specyficznych działań związanych z danym eventem.
        public abstract Task ExecuteEvent();

        // Metoda do realizacji działań związanych z zakończeniem eventu.
        public abstract Task EndEvent();

        // Metoda do wczytania ustawień konkretnego eventu z konfiguracji.
        public abstract Task LoadEventSettings(EventSystemConfig config);

        // Dodaje gracza do listy uczestników eventu.
        public abstract Task AddPlayer(long steamId);

        // Usuwa gracza z listy uczestników eventu.
        public abstract Task RemovePlayer(long steamId);

        // Sprawdza, czy gracz jest w liście uczestników eventu.
        public abstract Task<bool> IsPlayerParticipating(long steamId);

        // Sprawdza postępy gracza w evencie.
        public abstract Task CheckPlayerProgress(long steamId);

        // Sprawdza, czy event jest aktywny w danym momencie.
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


        // Oblicza czas, który pozostał do rozpoczęcia eventu.
        public TimeSpan GetNextStartTime(DateTime now)
        {
            var startOfDay = now.Date.Add(StartTime);
            return now < startOfDay ? startOfDay - now : TimeSpan.Zero;
        }

        // Oblicza czas, który pozostał do zakończenia eventu.
        public TimeSpan GetNextEndTime(DateTime now)
        {
            var endOfDay = now.Date.Add(EndTime);
            return now < endOfDay ? endOfDay - now : TimeSpan.Zero;
        }

        // Przyznaje nagrodę graczowi.
        public virtual async Task AwardPlayer(long steamId, long points)
        {
            // Pobierz menedżera bazy danych z głównego pluginu
            var databaseManager = EventSystemMain.Instance.DatabaseManager;

            // Pobierz konfigurację
            var config = EventSystemMain.Instance.Config;

            if (config.UseDatabase)
            {
                // Logika nagradzania w bazie danych
                databaseManager.UpdatePlayerPoints(steamId.ToString(), points);
            }
            else
            {
                // Pobierz menedżera kont graczy z głównego pluginu
                var xmlManager = EventSystemMain.Instance.PlayerAccountXmlManager;
                bool updateResult = await xmlManager.UpdatePlayerPointsAsync(steamId, points).ConfigureAwait(false);
                if (!updateResult)
                {
                    // Obsługa sytuacji, gdy konto gracza nie istnieje (jeśli to konieczne)
                }
            }
        }

        // Metody do zarządzania dodatkowymi elementami eventu, np. siatkami, obiektami.
        public virtual Task SpawnGrid() { /* implementacja */ return Task.CompletedTask; }
        public virtual Task ManageGrid() { /* implementacja */ return Task.CompletedTask; }
        public virtual Task CleanupGrid() { /* implementacja */ return Task.CompletedTask; }

        // Sprawdza, czy event jest aktywny w określonym dniu miesiąca.
        public bool IsActiveOnDayOfMonth(int day)
        {
            // Zwraca true, jeśli wydarzenie jest aktywne w określonym dniu miesiąca
            return ActiveDaysOfMonth.Count == 0 || ActiveDaysOfMonth.Contains(day);
        }
    }
}
