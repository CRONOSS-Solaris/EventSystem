using NLog.Fluent;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSystem.Events
{
    public abstract class EventsBase
    {
        public string EventName { get; set; }
        public bool IsEnabled { get; set; } = true;
        public List<int> ActiveDaysOfMonth { get; set; } = new List<int>();
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // Abstrakcyjna metoda do wykonania specyficznych działań związanych z danym eventem.
        public abstract Task ExecuteEvent();

        // Abstrakcyjna metoda do realizacji działań związanych z zakończeniem eventu.
        public abstract Task EndEvent();

        // Abstrakcyjna metoda do wczytania ustawień konkretnego eventu z konfiguracji.
        public abstract Task LoadEventSettings(EventSystemConfig config);

        // Sprawdza, czy event jest aktywny w danym momencie na podstawie aktualnej daty i godziny.
        public bool IsActiveNow()
        {
            var now = DateTime.Now;
            bool isActiveToday = ActiveDaysOfMonth.Contains(now.Day);
            bool isActiveTime = now.TimeOfDay >= StartTime && now.TimeOfDay <= EndTime;

            return IsEnabled && isActiveToday && isActiveTime;
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

        // Sprawdza, czy event jest aktywny w określonym dniu tygodnia.
        public bool IsActiveOnDayOfMonth(int day)
        {
            // Zwraca true, jeśli wydarzenie jest aktywne w określonym dniu miesiąca
            return ActiveDaysOfMonth.Count == 0 || ActiveDaysOfMonth.Contains(day);
        }

        // Loguje szczegółowe informacje o wydarzeniu, w tym nazwę, status, dni aktywne, oraz godziny startu i końca.
        public Task LogEventDetails()
        {
            string activeDaysText = ActiveDaysOfMonth.Count > 0 ? string.Join(", ", ActiveDaysOfMonth) : "Every day";
            string status = IsEnabled ? "Enabled" : "Disabled";

            Log.Error($"Event Details - Name: {EventName}, Status: {status}, Active Days of Month: {activeDaysText}, Start Time: {StartTime}, End Time: {EndTime}");

            return Task.CompletedTask;
        }
    }
}