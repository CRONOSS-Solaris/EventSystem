using EventSystem.Utils;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VRageMath;

namespace EventSystem.Events
{
    public abstract partial class EventsBase
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/EventsBase");

        // New property indicating whether to use an event-specific configuration (Do not change to false when creating an event outside of the source code)
        public bool UseEventSpecificConfig { get; set; } = true;

        //location of the profile with bluepirnt grids
        protected virtual string PrefabStoragePath { get; set; }

        // The name of the event, it can be set in derived classes.
        public string EventName { get; set; }

        // Determines whether the event is enabled.
        public bool IsEnabled { get; set; }

        // List of days of the month when the event is active.
        public List<int> ActiveDaysOfMonth { get; set; }

        // Event start time.
        public TimeSpan StartTime { get; set; }

        // End time of the event.
        public TimeSpan EndTime { get; set; }

        // Method to perform specific actions related to an event.
        public abstract Task SystemStartEvent();

        //Method of starting an event
        public abstract Task StartEvent();

        // Method to implement activities related to the end of the event.
        public abstract Task SystemEndEvent();

        // Method to load the settings of a specific event from the configuration.
        public virtual Task LoadEventSettings(EventSystemConfig config)
        {
            return Task.CompletedTask;
        }

        public virtual void LoadEventSpecificSettings()
        {

        }

        // Checks if the event is active on the specified day of the month.
        public bool IsActiveOnDayOfMonth(int day)
        {
            // Returns true if the event is active on the specified day of the month
            return ActiveDaysOfMonth.Count == 0 || ActiveDaysOfMonth.Contains(day);
        }

        // Checks if the event is active at the moment.
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


        // Calculates the time left until the event starts.
        public TimeSpan GetNextStartTime(DateTime now)
        {
            var startOfDay = now.Date.Add(StartTime);
            return now < startOfDay ? startOfDay - now : TimeSpan.Zero;
        }

        // Calculates the time remaining in the event.
        public TimeSpan GetNextEndTime(DateTime now)
        {
            var endOfDay = now.Date.Add(EndTime);
            return now < endOfDay ? endOfDay - now : TimeSpan.Zero;
        }
    }
}
