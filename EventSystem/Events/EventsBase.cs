using EventSystem.Utils;
using NLog;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Concurrent;

namespace EventSystem.Events
{
    [ProtoContract]
    public abstract partial class EventsBase
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/EventsBase");

        /// <summary>
        /// Określa, czy używać konfiguracji specyficznej dla eventu.
        /// </summary>
        public bool UseEventSpecificConfig { get; set; } = true;

        /// <summary>
        /// Ścieżka, gdzie są przechowywane blueprinty gridów dla eventu.
        /// </summary>
        protected virtual string PrefabStoragePath { get; set; } = Path.Combine("EventSystem", "EventPrefabBlueprint");

        /// <summary>
        /// Nazwa eventu.
        /// </summary>
        [ProtoMember(1)]
        public string EventName { get; set; }

        /// <summary>
        /// Określa, czy event jest włączony.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Lista dni miesiąca, w których event jest aktywny.
        /// </summary>
        public List<int> ActiveDaysOfMonth { get; set; }

        /// <summary>
        /// Czas rozpoczęcia eventu.
        /// </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>
        /// Czas zakończenia eventu.
        /// </summary>
        public TimeSpan EndTime { get; set; }

        /// <summary>
        /// Opis eventu.
        /// </summary>
        public abstract string EventDescription { get; }

        /// <summary>
        /// Wykonuje specyficzne akcje związane z rozpoczęciem eventu.
        /// </summary>
        /// <returns>Zadanie reprezentujące asynchroniczną operację.</returns>
        public abstract Task SystemStartEvent();

        /// <summary>
        /// Uruchamia event.
        /// </summary>
        /// <returns>Zadanie reprezentujące asynchroniczną operację.</returns>
        public virtual Task StartEvent()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Wykonuje specyficzne akcje związane z zakończeniem eventu.
        /// </summary>
        /// <returns>Zadanie reprezentujące asynchroniczną operację.</returns>
        public abstract Task SystemEndEvent();

        /// <summary>
        /// Ładuje ustawienia specyficzne dla eventu z konfiguracji.
        /// </summary>
        /// <param name="config">Konfiguracja do załadowania ustawień.</param>
        /// <returns>Zadanie reprezentujące asynchroniczną operację.</returns>
        public virtual Task LoadEventSettings(EventSystemConfig config)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Ładuje ustawienia specyficzne dla eventu.
        /// </summary>
        public virtual void LoadEventSpecificSettings()
        {

        }

        /// <summary>
        /// Sprawdza, czy event jest aktywny w danym dniu miesiąca.
        /// </summary>
        /// <param name="day">Dzień miesiąca do sprawdzenia.</param>
        /// <returns>True, jeśli event jest aktywny w danym dniu, w przeciwnym razie false.</returns>
        public bool IsActiveOnDayOfMonth(int day)
        {
            // Zwraca true, jeśli event jest aktywny w danym dniu miesiąca
            return ActiveDaysOfMonth.Count == 0 || ActiveDaysOfMonth.Contains(day);
        }

        /// <summary>
        /// Sprawdza, czy event jest obecnie aktywny.
        /// </summary>
        /// <returns>True, jeśli event jest obecnie aktywny, w przeciwnym razie false.</returns>
        public bool IsActiveNow()
        {
            var now = DateTime.Now;
            bool isActiveToday = ActiveDaysOfMonth.Count == 0 || ActiveDaysOfMonth.Contains(now.Day);
            bool isActiveTime = now.TimeOfDay >= StartTime && now.TimeOfDay <= EndTime;
            bool isActive = IsEnabled && isActiveToday && isActiveTime;

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Sprawdzanie, czy '{EventName}' jest aktywny teraz:");
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Aktualny czas: {now}");
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"IsEnabled: {IsEnabled}");
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Aktywny dzisiaj ({now.Day}): {isActiveToday}");
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Aktywny czas ({now.TimeOfDay}): {isActiveTime}");
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Czy aktywny: {isActive}");

            return isActive;
        }

        /// <summary>
        /// Oblicza czas pozostały do rozpoczęcia eventu.
        /// </summary>
        /// <param name="now">Aktualna data i czas.</param>
        /// <returns>Czas pozostały do rozpoczęcia eventu.</returns>
        public TimeSpan GetNextStartTime(DateTime now)
        {
            var startOfDay = now.Date.Add(StartTime);
            return now < startOfDay ? startOfDay - now : TimeSpan.Zero;
        }

        /// <summary>
        /// Oblicza czas pozostały do zakończenia eventu.
        /// </summary>
        /// <param name="now">Aktualna data i czas.</param>
        /// <returns>Czas pozostały do zakończenia eventu.</returns>
        public TimeSpan GetNextEndTime(DateTime now)
        {
            var endOfDay = now.Date.Add(EndTime);
            return now < endOfDay ? endOfDay - now : TimeSpan.Zero;
        }

        /// <summary>
        /// Aktualny stan eventu.
        /// </summary>
        public EventState State { get; set; }

        private static readonly string StateDirectory = Path.Combine(EventSystemMain.Instance.StoragePath, "EventSystem", "EventTechnical_ReadOnly");

        /// <summary>
        /// Zapisuje pełny stan eventu, włączając dane dynamiczne, do pliku JSON.
        /// </summary>
        public virtual void SaveFullState()
        {
            try
            {
                if (!Directory.Exists(StateDirectory))
                {
                    Directory.CreateDirectory(StateDirectory);
                }

                string stateFilePath = Path.Combine(StateDirectory, $"{EventName}_fullstate.json");

                // Pobierz dynamiczne dane stanu
                var dynamicData = GetEventStateData();

                // Dodaj identyfikatory safezon i siatek do dynamicznych danych stanu
                var eventState = new EventStateData
                {
                    State = this.State,
                    DynamicData = new
                    {
                        EventSpecificData = dynamicData,
                        SafeZoneEntityIds = safezoneEntityIds.Keys.ToList(),
                        SpawnedGridEntityIds = SpawnedGridsEntityIds.Keys.ToList()
                    }
                };

                string json = JsonConvert.SerializeObject(eventState, Formatting.Indented);
                File.WriteAllText(stateFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Błąd podczas zapisywania pełnego stanu dla eventu '{EventName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Ładuje pełny stan eventu, włączając dane dynamiczne, z pliku JSON.
        /// </summary>
        public virtual void LoadFullState()
        {
            try
            {
                string stateFilePath = Path.Combine(StateDirectory, $"{EventName}_fullstate.json");
                if (File.Exists(stateFilePath))
                {
                    string json = File.ReadAllText(stateFilePath);
                    var eventState = JsonConvert.DeserializeObject<EventStateData>(json);

                    this.State = eventState.State;

                    if (eventState.DynamicData != null)
                    {
                        // Odczytaj identyfikatory safezon i siatek
                        var dynamicData = eventState.DynamicData as Newtonsoft.Json.Linq.JObject;
                        var eventSpecificData = dynamicData["EventSpecificData"];
                        var safeZoneIds = dynamicData["SafeZoneEntityIds"]?.ToObject<List<long>>() ?? new List<long>();
                        var spawnedGridIds = dynamicData["SpawnedGridEntityIds"]?.ToObject<List<long>>() ?? new List<long>();

                        // Ustaw dane specyficzne dla eventu
                        SetEventStateData(eventSpecificData);

                        // Przywróć identyfikatory safezon
                        safezoneEntityIds.Clear();
                        foreach (var id in safeZoneIds)
                        {
                            safezoneEntityIds.TryAdd(id, true);
                        }

                        // Przywróć identyfikatory zespawnowanych siatek
                        SpawnedGridsEntityIds.Clear();
                        foreach (var id in spawnedGridIds)
                        {
                            SpawnedGridsEntityIds.TryAdd(id, true);
                        }
                    }
                }
                else
                {
                    // Brak zapisanego stanu, inicjalizacja jako zaplanowany
                    this.State = EventState.Scheduled;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Błąd podczas wczytywania pełnego stanu dla eventu '{EventName}': {ex.Message}");
                this.State = EventState.Scheduled; // Domyślny stan w przypadku błędu
            }
        }


        /// <summary>
        /// Usuwa zapisany pełny stan eventu.
        /// </summary>
        public virtual void DeleteFullState()
        {
            try
            {
                string stateFilePath = Path.Combine(StateDirectory, $"{EventName}_fullstate.json");
                if (File.Exists(stateFilePath))
                {
                    File.Delete(stateFilePath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Błąd podczas usuwania pełnego stanu dla eventu '{EventName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Pobiera dynamiczne dane stanu eventu do zapisania.
        /// Ta metoda powinna być nadpisana w klasach pochodnych, aby uwzględnić specyficzne dane stanu.
        /// </summary>
        /// <returns>Obiekt reprezentujący dynamiczne dane stanu.</returns>
        protected virtual object GetEventStateData()
        {
            // Nadpisz w klasach pochodnych, aby zwrócić specyficzne dane stanu
            return null;
        }

        /// <summary>
        /// Ustawia dynamiczne dane stanu eventu z wczytanych danych.
        /// Ta metoda powinna być nadpisana w klasach pochodnych, aby przywrócić specyficzne dane stanu.
        /// </summary>
        /// <param name="data">Dynamiczne dane stanu wczytane z zapisanego stanu.</param>
        protected virtual void SetEventStateData(object data)
        {
            // Nadpisz w klasach pochodnych, aby ustawić specyficzne dane stanu
        }

        /// <summary>
        /// Przywraca stan eventu po restarcie lub awarii serwera.
        /// </summary>
        /// <returns>Zadanie reprezentujące asynchroniczną operację.</returns>
        public virtual async Task RestoreEvent()
        {
            // Domyślna implementacja: wywołaj SystemStartEvent bez ponownego inicjalizowania
            await SystemStartEvent();
        }

        [ProtoContract]
        protected class EventStateData
        {
            [ProtoMember(1)]
            public EventState State { get; set; }

            [ProtoMember(1)]
            public object DynamicData { get; set; }
        }
    }
}
