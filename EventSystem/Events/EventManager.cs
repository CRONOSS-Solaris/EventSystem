using EventSystem.Managers;
using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading;

namespace EventSystem.Events
{
    public class EventManager
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/EventManager");
        private readonly List<EventsBase> _events = new List<EventsBase>();
        private readonly Dictionary<string, Timer> _startTimers = new Dictionary<string, Timer>();
        private readonly Dictionary<string, Timer> _endTimers = new Dictionary<string, Timer>();
        private readonly EventSystemConfig _config;
        private readonly LCDManager _lcdManager;
        private AllEventsLCDManager _allEventsLcdManager;

        public EventManager(EventSystemConfig config, LCDManager lcdManager, AllEventsLCDManager allEventsLcdManager)
        {
            _config = config;
            _lcdManager = lcdManager;
            _allEventsLcdManager = allEventsLcdManager;
        }

        public void RegisterEvent(EventsBase eventItem)
        {
            try
            {
                _events.Add(eventItem);
                eventItem.LoadEventSettings(_config);
                UpdateLCDs();
                LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' successfully registered");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error while registering event '{eventItem.EventName}': {ex.Message}");
            }
        }

        public void ScheduleEvent(EventsBase eventItem)
        {
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
                            var endTimer = new Timer(EndEvent, eventItem, endTime, Timeout.InfiniteTimeSpan);
                            _endTimers[eventItem.EventName] = endTimer;
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


        private async void StartEvent(object state)
        {
            var eventItem = (EventsBase)state;
            try
            {
                await eventItem.ExecuteEvent();
                await eventItem.LogEventDetails();
                UpdateLCDs();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error during StartEvent for '{eventItem.EventName}': {ex.Message}");
            }
        }

        private async void EndEvent(object state)
        {
            var eventItem = (EventsBase)state;
            try
            {
                await eventItem.EndEvent();
                await eventItem.LogEventDetails();
                UpdateLCDs();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error during EndEvent for '{eventItem.EventName}': {ex.Message}");
            }
        }

        private void UpdateLCDs()
        {
            try
            {
                _lcdManager.UpdateMonitorBlocks();
                _allEventsLcdManager.UpdateMonitorBlocks();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error while updating LCDs: {ex.Message}");
            }
        }

        public IEnumerable<EventsBase> Events => _events;
    }
}
