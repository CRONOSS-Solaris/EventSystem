using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSystem.Events
{
    public abstract class EventsBase
    {
        public string EventName { get; set; }
        public bool IsEnabled { get; set; } = true;
        public List<DayOfWeek> ActiveDays { get; set; } = new List<DayOfWeek>();
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // Metoda abstrakcyjna do wykonania konkretnego eventu
        public abstract Task ExecuteEvent();

        // Metoda abstrakcyjna do zakończenia konkretnego eventu
        public abstract Task EndEvent();

        // Metoda do sprawdzenia, czy event jest aktywny w danej chwili
        public bool IsActiveNow()
        {
            var now = DateTime.Now;

            bool isActiveEveryday = ActiveDays.Count == 0;

            return IsEnabled &&
                   (isActiveEveryday || ActiveDays.Contains(now.DayOfWeek)) &&
                   now.TimeOfDay >= StartTime &&
                   now.TimeOfDay <= EndTime;
        }

        public TimeSpan GetNextStartTime(DateTime now)
        {
            var startOfDay = now.Date.Add(StartTime);
            return now < startOfDay ? startOfDay - now : TimeSpan.Zero;
        }

        public TimeSpan GetNextEndTime(DateTime now)
        {
            var endOfDay = now.Date.Add(EndTime);
            return now < endOfDay ? endOfDay - now : TimeSpan.Zero;
        }

        public bool IsActiveOnDay(DayOfWeek day)
        {
            // Zwraca true, jeśli wydarzenie jest aktywne codziennie lub w określonym dniu tygodnia
            return ActiveDays.Count == 0 || ActiveDays.Contains(day);
        }

        // Metoda do logowania szczegółów wydarzenia
        public void LogEventDetails()
        {
            // Logika do logowania szczegółów wydarzenia
        }

        // Metoda do wczytania ustawień konkretnego eventu z konfiguracji
        public virtual Task LoadEventSettings(EventSystemConfig config)
        {
            // Tutaj możesz dodać logikę wczytywania ustawień z konfiguracji
            // Możesz użyć "config" do dostępu do ustawień i zaktualizowania pól klasy
            return Task.CompletedTask;
        }

    }
}
