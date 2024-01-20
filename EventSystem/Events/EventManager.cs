using EventSystem.Utils;
using NLog;
using Sandbox.Engine.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EventSystem.Events
{
    public class EventManager
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly List<EventsBase> _events = new List<EventsBase>();
        private readonly EventSystemConfig _config;

        public EventManager(EventSystemConfig config)
        {
            _config = config;
        }

        public void RegisterEvent(EventsBase eventItem)
        {
            _events.Add(eventItem);
            eventItem.LoadEventSettings(_config);
            LoggerHelper.DebugLog(Log, _config, $"Event '{eventItem.EventName}' registered.");
        }

        public IEnumerable<EventsBase> Events => _events;

        public void ExecuteEvent(string eventName)
        {
            var eventToExecute = _events.FirstOrDefault(e => e.EventName == eventName);
            if (eventToExecute != null && eventToExecute.IsActiveNow())
            {
                eventToExecute.ExecuteEvent();
                LoggerHelper.DebugLog(Log, _config, $"Event '{eventName}' executed.");
            }
            else
            {
                Log.Warn($"Event {eventName} is either not active now, disabled, or not found.");
            }
        }
    }
}
