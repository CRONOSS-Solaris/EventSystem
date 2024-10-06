using DSharpPlus.Entities;
using EventSystem.Discord;
using EventSystem.Discord.Utils;
using EventSystem.Managers;
using EventSystem.Utils;
using NLog;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Torch;
using Torch.API.Managers;
using Torch.Commands;

namespace EventSystem.Events
{
    public enum EventState
    {
        Scheduled,
        Running,
        Ended
    }

    public class EventManager
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/EventManager");
        private readonly List<EventsBase> _events = new List<EventsBase>();
        private readonly Dictionary<string, Timer> _startTimers = new Dictionary<string, Timer>();
        private readonly Dictionary<string, Timer> _endTimers = new Dictionary<string, Timer>();
        private readonly EventSystemConfig _config;
        private readonly ActiveEventsLCDManager _activeEventsLCDManager;
        private readonly AllEventsLCDManager _allEventsLcdManager;
        private readonly MessageService _messageService;
        private readonly object _eventStateLock = new object();

        public EventManager(EventSystemConfig config, ActiveEventsLCDManager lcdManager, AllEventsLCDManager allEventsLcdManager, MessageService messageService)
        {
            _config = config;
            _activeEventsLCDManager = lcdManager;
            _allEventsLcdManager = allEventsLcdManager;
            _messageService = messageService;
        }

        public void RegisterEvent(EventsBase eventItem)
        {
            try
            {
                if (string.IsNullOrEmpty(eventItem.EventName))
                {
                    throw new InvalidOperationException($"Event '{eventItem.GetType().Name}' cannot be registered without an EventName.");
                }

                if (_events.Any(e => e.EventName.Equals(eventItem.EventName, StringComparison.OrdinalIgnoreCase)))
                {
                    Log.Error($"Event with the name '{eventItem.EventName}' is already registered.");
                    return;
                }

                eventItem.LoadFullState();

                _events.Add(eventItem);

                if (eventItem.UseEventSpecificConfig)
                {
                    eventItem.LoadEventSpecificSettings();
                }
                else
                {
                    eventItem.LoadEventSettings(_config);
                }

                LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' successfully registered");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error while registering event '{eventItem.GetType().Name}': {ex.Message}");
            }
        }

        public void InitializeEvents()
        {
            foreach (var eventItem in _events)
            {
                // Wczytaj pełny stan, włączając dane dynamiczne
                eventItem.LoadFullState();

                if (eventItem.State == EventState.Running)
                {
                    LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' był uruchomiony podczas awarii serwera. Przywracanie go teraz.");

                    // Uruchom event bez ponownej inicjalizacji wszystkiego
                    StartEvent(eventItem, restore: true);
                }
                else if (eventItem.State == EventState.Scheduled)
                {
                    LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' jest zaplanowany. Ponowne planowanie.");
                    ScheduleEvent(eventItem);
                }
            }

            UpdateLCDs();
        }

        public void ScheduleEvent(EventsBase eventItem)
        {
            lock (_eventStateLock)
            {
                if (!eventItem.IsEnabled)
                {
                    LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' jest wyłączony i nie zostanie zaplanowany.");
                    return;
                }

                if (eventItem.State == EventState.Ended)
                {
                    LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' już się zakończył.");
                    return;
                }

                var now = DateTime.Now;
                var dayOfMonth = now.Day;

                if (eventItem.IsActiveOnDayOfMonth(dayOfMonth))
                {
                    var startTime = eventItem.GetNextStartTime(now);
                    var endTime = eventItem.GetNextEndTime(now);

                    try
                    {
                        if (now > now.Date.Add(eventItem.StartTime) && now < now.Date.Add(eventItem.EndTime))
                        {
                            StartEvent(eventItem);
                        }
                        else
                        {
                            if (startTime > TimeSpan.Zero)
                            {
                                var startTimer = new Timer(StartEvent, eventItem, startTime, Timeout.InfiniteTimeSpan);
                                _startTimers[eventItem.EventName] = startTimer;
                                eventItem.State = EventState.Scheduled;
                                eventItem.SaveFullState();
                            }

                            if (endTime > TimeSpan.Zero)
                            {
                                if (_endTimers.ContainsKey(eventItem.EventName))
                                {
                                    _endTimers[eventItem.EventName].Change(endTime, Timeout.InfiniteTimeSpan);
                                }
                                else
                                {
                                    var endTimer = new Timer(EndEvent, eventItem, endTime, Timeout.InfiniteTimeSpan);
                                    _endTimers[eventItem.EventName] = endTimer;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Error while scheduling event '{eventItem.EventName}': {ex.Message}");
                    }
                }
                UpdateLCDs();
            }
        }

        private void StartEvent(object state)
        {
            StartEvent(state, restore: false);
        }

        private void StartEvent(object state, bool restore)
        {
            var eventItem = (EventsBase)state;
            LoggerHelper.DebugLog(Log, _config, $"Próba uruchomienia eventu '{eventItem.EventName}'.");

            lock (_eventStateLock)
            {
                eventItem.State = EventState.Running;
                eventItem.SaveFullState();
            }

            Task.Run(async () =>
            {
                if (restore)
                {
                    // Jeśli przywracamy, uruchom event bez ponownej inicjalizacji
                    await eventItem.RestoreEvent();
                }
                else
                {
                    // Normalne uruchomienie eventu
                    await eventItem.SystemStartEvent();
                }
            }).ContinueWith(async task =>
            {
                if (task.IsFaulted)
                {
                    var exception = task.Exception?.InnerException?.Message ?? "Unknown error";
                    Log.Error($"Błąd podczas uruchamiania eventu '{eventItem.EventName}': {exception}");
                }
                else
                {
                    SendNotification($"{eventItem.EventName} rozpoczyna się teraz!", "Green");
                    string endTime = $"{eventItem.EndTime:hh\\:mm\\:ss}";
                    await _messageService.SendEmbedMessageToAllRegisteredUsers($"⏰ {eventItem.EventName} rozpoczyna się teraz! ⏰", $"Dołącz do nas! Event zakończy się o {endTime} 🎉", DiscordColor.Green);
                    LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' został pomyślnie uruchomiony.");
                }

                UpdateLCDs();
            });
        }

        private void EndEvent(object state)
        {
            var eventItem = (EventsBase)state;
            LoggerHelper.DebugLog(Log, _config, $"Próba zakończenia eventu '{eventItem.EventName}'.");

            lock (_eventStateLock)
            {
                eventItem.State = EventState.Ended;
                eventItem.SaveFullState();
            }

            Task.Run(() => eventItem.SystemEndEvent()).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    var exception = task.Exception?.InnerException?.Message ?? "Unknown error";
                    LoggerHelper.DebugLog(Log, _config, $"Błąd podczas zakończania eventu '{eventItem.EventName}': {exception}");
                }
                else
                {
                    SendNotification($"{eventItem.EventName} zakończył się. Dziękujemy za udział!", "Red");
                    LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' został pomyślnie zakończony.");
                }

                UpdateLCDs();
            });
        }

        private void UpdateLCDs()
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                try
                {
                    _activeEventsLCDManager.UpdateMonitorBlocks();
                    _allEventsLcdManager.UpdateMonitorBlocks();
                    LoggerHelper.DebugLog(Log, _config, "LCD zostały zaktualizowane pomyślnie.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Błąd podczas aktualizacji LCD: {ex.Message}");
                    LoggerHelper.DebugLog(Log, _config, $"Błąd podczas aktualizacji LCD: {ex.Message}");
                }
            });
        }

        private void SendNotification(string message, string color)
        {
            var torch = TorchBase.Instance;
            if (torch == null)
            {
                Log.Error("TorchBase.Instance jest null. Powiadomienie nie może zostać wysłane.");
                return;
            }

            var session = torch.CurrentSession;
            if (session == null)
            {
                Log.Error("TorchBase.CurrentSession jest null. Powiadomienie nie może zostać wysłane.");
                return;
            }

            var commandManager = session.Managers.GetManager<CommandManager>();
            if (commandManager == null)
            {
                Log.Error("CommandManager jest null. Powiadomienie nie może zostać wysłane.");
                return;
            }

            string notificationCommand = $"!notify \"{message}\" 9000 {color}";
            commandManager.HandleCommandFromServer(notificationCommand);
            Log.Info($"Wysłano powiadomienie: {message} z kolorem {color}.");
        }

        public IEnumerable<EventsBase> Events => _events;
    }
}
