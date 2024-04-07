using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace EventSystem.Events
{
    public abstract partial class EventsBase
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/EventsBase");

        /// <summary>
        /// Gets or sets a value indicating whether to use event-specific configuration.
        /// </summary>
        public bool UseEventSpecificConfig { get; set; } = true;

        /// <summary>
        /// Gets or sets the path where blueprint grids are stored for the event.
        /// </summary>
        protected virtual string PrefabStoragePath { get; set; } = Path.Combine("EventSystem", "EventPrefabBlueprint");

        /// <summary>
        /// Gets or sets the name of the event.
        /// </summary>
        public string EventName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the event is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets a list of days of the month when the event is active.
        /// </summary>
        public List<int> ActiveDaysOfMonth { get; set; }

        /// <summary>
        /// Gets or sets the start time of the event.
        /// </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time of the event.
        /// </summary>
        public TimeSpan EndTime { get; set; }

        /// <summary>
        /// Gets the description of the event.
        /// </summary>
        public abstract string EventDescription { get; }

        /// <summary>
        /// Performs specific actions related to starting the event.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task SystemStartEvent();

        /// <summary>
        /// Starts the event.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual Task StartEvent()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs specific actions related to ending the event.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task SystemEndEvent();

        /// <summary>
        /// Loads the settings of a specific event from the configuration.
        /// </summary>
        /// <param name="config">The configuration to load settings from.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual Task LoadEventSettings(EventSystemConfig config)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Loads event-specific settings.
        /// </summary>
        public virtual void LoadEventSpecificSettings()
        {

        }

        /// <summary>
        /// Checks if the event is active on the specified day of the month.
        /// </summary>
        /// <param name="day">The day of the month to check.</param>
        /// <returns>True if the event is active on the specified day, otherwise false.</returns>
        public bool IsActiveOnDayOfMonth(int day)
        {
            // Returns true if the event is active on the specified day of the month
            return ActiveDaysOfMonth.Count == 0 || ActiveDaysOfMonth.Contains(day);
        }

        /// <summary>
        /// Checks if the event is active at the current moment.
        /// </summary>
        /// <returns>True if the event is currently active, otherwise false.</returns>
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


        /// <summary>
        /// Calculates the time left until the event starts.
        /// </summary>
        /// <param name="now">The current date and time.</param>
        /// <returns>The time left until the event starts.</returns>
        public TimeSpan GetNextStartTime(DateTime now)
        {
            var startOfDay = now.Date.Add(StartTime);
            return now < startOfDay ? startOfDay - now : TimeSpan.Zero;
        }

        /// <summary>
        /// Calculates the time remaining in the event.
        /// </summary>
        /// <param name="now">The current date and time.</param>
        /// <returns>The time remaining in the event.</returns>
        public TimeSpan GetNextEndTime(DateTime now)
        {
            var endOfDay = now.Date.Add(EndTime);
            return now < endOfDay ? endOfDay - now : TimeSpan.Zero;
        }
    }
}
