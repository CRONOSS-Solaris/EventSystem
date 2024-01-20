using System;
using System.Collections.Generic;

namespace EventSystem.Events
{
    public abstract class EventsBase
    {
        public string Name { get; set; }
        public bool IsEnabled { get; set; } = true;
        public List<DayOfWeek> ActiveDays { get; set; } = new List<DayOfWeek>();
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // Metoda abstrakcyjna do wykonania konkretnego eventu
        public abstract void ExecuteEvent();

        // Metoda do sprawdzenia, czy event jest aktywny w danej chwili
        public bool IsActiveNow()
        {
            var now = DateTime.Now;
            // Sprawdź, czy event jest aktywny codziennie, jeśli lista ActiveDays jest pusta
            bool isActiveEveryday = ActiveDays.Count == 0;

            return IsEnabled &&
                   (isActiveEveryday || ActiveDays.Contains(now.DayOfWeek)) &&
                   now.TimeOfDay >= StartTime &&
                   now.TimeOfDay <= EndTime;
        }

        // Metoda do logowania szczegółów wydarzenia
        public void LogEventDetails()
        {
            // Logika do logowania szczegółów wydarzenia
        }

        // Metoda do wczytania ustawień konkretnego eventu z konfiguracji
        public virtual void LoadEventSettings(EventSystemConfig config)
        {
            // Tutaj możesz dodać logikę wczytywania ustawień z konfiguracji
            // Możesz użyć "config" do dostępu do ustawień i zaktualizowania pól klasy
        }
    }
}
