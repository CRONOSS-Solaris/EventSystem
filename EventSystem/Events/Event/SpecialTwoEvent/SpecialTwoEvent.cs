//using EventSystem.Events;
//using EventSystem.Utils;
//using NLog;
//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;

//namespace EventSystem.Event
//{
//    public class SpecialTwoEvent : EventsBase
//    {
//        public static readonly Logger Log = LogManager.GetLogger("EventSystem/SpecialTwoEvent");
//        private readonly EventSystemConfig _config;

//        public SpecialTwoEvent(EventSystemConfig config)
//        {
//            _config = config;
//            AllowParticipationInOtherEvents = true;
//            EventName = "SpecialTwoEvent";
//        }

//        public override Task SystemStartEvent()
//        {
//            // Implementacja logiki wydarzenia
//            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"System Start SpecialTwoEvent.");
//            return Task.CompletedTask;
//        }

//        public override async Task StartEvent()
//        {
//            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Start SpecialTwoEvent.");
//        }

//        public override Task SystemEndEvent()
//        {
//            // Implementacja logiki końca wydarzenia
//            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Ending SpecialTwoEvent.");
//            return Task.CompletedTask;
//        }

//        public override Task CheckPlayerProgress(long steamId)
//        {
//            return Task.CompletedTask;
//        }

//        public override void LoadEventSpecificSettings()
//        {
//            IsEnabled = false;
//            ActiveDaysOfMonth = new List<int> { 6, 15, 20 };
//            StartTime = TimeSpan.Parse("00:00:00");
//            EndTime = TimeSpan.Parse("23:59:59");

//            string activeDaysText = ActiveDaysOfMonth.Count > 0 ? string.Join(", ", ActiveDaysOfMonth) : "Every day";
//            LoggerHelper.DebugLog(Log, _config, $"Loaded SpecialTwoEvent settings: IsEnabled={IsEnabled}, Active Days of Month={activeDaysText}, StartTime={StartTime}, EndTime={EndTime}");

//        }

//    }
//}
