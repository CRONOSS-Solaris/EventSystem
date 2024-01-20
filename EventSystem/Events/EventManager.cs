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

        public EventManager(EventSystemConfig config, LCDManager lcdManager)
        {
            _config = config;
            _lcdManager = lcdManager;
        }

        public void RegisterEvent(EventsBase eventItem)
        {
            try
            {
                _events.Add(eventItem);
                eventItem.LoadEventSettings(_config);
                ScheduleEvent(eventItem);
                LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' successfully registered and scheduled.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error while registering event '{eventItem.EventName}': {ex.Message}");
            }
        }


        private void ScheduleEvent(EventsBase eventItem)
        {
            var now = DateTime.Now;

            if (eventItem.IsActiveOnDay(now.DayOfWeek))
            {
                var startTime = eventItem.GetNextStartTime(now);
                var endTime = eventItem.GetNextEndTime(now);

                if (startTime > TimeSpan.Zero)
                {
                    var startTimer = new Timer(StartEvent, eventItem, startTime, Timeout.InfiniteTimeSpan);
                    _startTimers[eventItem.EventName] = startTimer;
                }

                if (endTime > TimeSpan.Zero)
                {
                    var endTimer = new Timer(EndEvent, eventItem, endTime, Timeout.InfiniteTimeSpan);
                    _endTimers[eventItem.EventName] = endTimer;
                }
            }
        }

        private async void StartEvent(object state)
        {
            var eventItem = (EventsBase)state;
            try
            {
                await eventItem.ExecuteEvent();
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
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error while updating LCDs: {ex.Message}");
            }
        }

        public IEnumerable<EventsBase> Events => _events;
    }
}
