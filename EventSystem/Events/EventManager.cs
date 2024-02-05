using EventSystem.Managers;
using EventSystem.Utils;
using NLog;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Torch;
using Torch.API.Managers;
using Torch.Commands;

namespace EventSystem.Events
{
    public class EventManager
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/EventManager");
        private readonly List<EventsBase> _events = new List<EventsBase>();
        private readonly Dictionary<string, Timer> _startTimers = new Dictionary<string, Timer>();
        private readonly Dictionary<string, Timer> _endTimers = new Dictionary<string, Timer>();
        private readonly EventSystemConfig _config;
        private readonly ActiveEventsLCDManager _activeEventsLCDManager;
        private AllEventsLCDManager _allEventsLcdManager;

        public EventManager(EventSystemConfig config, ActiveEventsLCDManager lcdManager, AllEventsLCDManager allEventsLcdManager)
        {
            _config = config;
            _activeEventsLCDManager = lcdManager;
            _allEventsLcdManager = allEventsLcdManager;
        }

        public void RegisterEvent(EventsBase eventItem)
        {
            try
            {
                _events.Add(eventItem);
                eventItem.LoadEventSettings(_config);
                LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' successfully registered");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error while registering event '{eventItem.EventName}': {ex.Message}");
            }
        }

        public void ScheduleEvent(EventsBase eventItem)
        {
            // Sprawdź, czy event jest włączony
            if (!eventItem.IsEnabled)
            {
                LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' is disabled and will not be scheduled.");
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
                    // Sprawdź, czy czas rozpoczęcia jest w przeszłości, a czas zakończenia w przyszłości
                    if (now > now.Date.Add(eventItem.StartTime) && now < now.Date.Add(eventItem.EndTime))
                    {
                        StartEvent(eventItem); // Uruchom event od razu
                    }
                    else
                    {
                        // Harmonogram rozpoczęcia eventu
                        if (startTime > TimeSpan.Zero)
                        {
                            var startTimer = new Timer(StartEvent, eventItem, startTime, Timeout.InfiniteTimeSpan);
                            _startTimers[eventItem.EventName] = startTimer;
                        }
                        // Harmonogram zakończenia eventu
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

        private void StartEvent(object state)
        {
            var eventItem = (EventsBase)state;
            LoggerHelper.DebugLog(Log, _config, $"Attempting to start event '{eventItem.EventName}'.");

            // Asynchroniczne wywołanie ExecuteEvent z obsługą callback
            Task.Run(() => eventItem.SystemStartEvent()).ContinueWith(task =>
            {
                // Wykonywane na wątku ThreadPool, dlatego wszelkie interakcje z UI lub elementami gry wymagają InvokeOnMainThread
                if (task.IsFaulted)
                {
                    // Logowanie błędów, jeśli takie wystąpiły
                    var exception = task.Exception?.InnerException?.Message ?? "Unknown error";
                    LoggerHelper.DebugLog(Log, _config, $"Error during starting event '{eventItem.EventName}': {exception}");
                }
                else
                {
                    SendNotification($"{eventItem.EventName} is starting now!", "Green");
                    // Sukces, można tutaj zaktualizować stan lub wykonać dodatkowe czynności
                    LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' started successfully.");
                }

                UpdateLCDs();
            });
        }


        private void EndEvent(object state)
        {
            var eventItem = (EventsBase)state;
            LoggerHelper.DebugLog(Log, _config, $"Attempting to end event '{eventItem.EventName}'.");

            // Asynchroniczne wywołanie EndEvent z obsługą callback
            Task.Run(() => eventItem.SystemEndEvent()).ContinueWith(task =>
            {
                // Wykonywane na wątku ThreadPool, dlatego wszelkie interakcje z UI lub elementami gry wymagają InvokeOnMainThread
                if (task.IsFaulted)
                {
                    // Logowanie błędów, jeśli takie wystąpiły
                    var exception = task.Exception?.InnerException?.Message ?? "Unknown error";
                    LoggerHelper.DebugLog(Log, _config, $"Error during ending event '{eventItem.EventName}': {exception}");
                }
                else
                {
                    SendNotification($"{eventItem.EventName} has ended. Thank you for participating!", "Red");
                    // Sukces, można tutaj zaktualizować stan lub wykonać dodatkowe czynności
                    LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' ended successfully.");
                }

                UpdateLCDs();
            });
        }

        private void UpdateLCDs()
        {
            // Wywołanie metody na głównym wątku gry
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                try
                {
                    _activeEventsLCDManager.UpdateMonitorBlocks();
                    _allEventsLcdManager.UpdateMonitorBlocks();
                    LoggerHelper.DebugLog(Log, _config, "LCDs updated successfully.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error while updating LCDs: {ex.Message}");
                    LoggerHelper.DebugLog(Log, _config, $"Error while updating LCDs: {ex.Message}");
                }
            });
        }

        private void SendNotification(string message, string color)
        {
            var torch = TorchBase.Instance;
            if (torch != null)
            {
                var commandManager = torch.CurrentSession.Managers.GetManager<CommandManager>();
                if (commandManager != null)
                {
                    string notificationCommand = $"!notify \"{message}\" 3000 {color}";
                    commandManager.HandleCommandFromServer(notificationCommand);
                }
            }
        }

        public IEnumerable<EventsBase> Events => _events;
    }
}
