using EventSystem.Events;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRageMath;

namespace EventSystem
{
    [Category("event")]
    public class PlayerEventSystemCommands : CommandModule
    {
        public EventSystemMain Plugin => (EventSystemMain)Context.Plugin;
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/PlayerEventSystemCommands");

        [Command("points", "Check your points.")]
        [Permission(MyPromoteLevel.None)]
        public void CheckPoints()
        {
            if (Context.Player == null)
            {
                Log.Error("This command can only be used by a player.");
                return;
            }

            long steamId = (long)Context.Player.SteamUserId;

            if (Plugin.Config.UseDatabase)
            {
                // Logika używania bazy danych
                var pointsResult = Plugin.DatabaseManager.GetPlayerPoints(steamId);
                if (pointsResult.HasValue)
                {
                    string response = new StringBuilder()
                        .AppendLine()
                        .AppendLine("---------------------")
                        .AppendLine("Available pts")
                        .AppendLine($"- {pointsResult.Value}")
                        .AppendLine("---------------------")
                        .ToString();

                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", response, Color.Green, Context.Player.SteamUserId);
                }
                else
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "Your account was not found in the database.", Color.Red, Context.Player.SteamUserId);
                }
            }
            else
            {
                var playerAccount = Plugin.PlayerAccountXmlManager.GetPlayerAccountAsync(steamId);
                if (playerAccount != null)
                {
                    string response = new StringBuilder()
                        .AppendLine()
                        .AppendLine("---------------------")
                        .AppendLine("Available pts")
                        .AppendLine($"- {playerAccount.Result}")
                        .AppendLine("---------------------")
                        .ToString();
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", response, Color.Green, Context.Player.SteamUserId);
                }
                else
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "You do not have an account file.", Color.Red, Context.Player.SteamUserId);
                }
            }
        }

        [Command("events", "Displays active and upcoming events.")]
        [Permission(MyPromoteLevel.None)]
        public void ShowEvents()
        {
            var eventManager = Plugin._eventManager;
            var activeEvents = eventManager.Events.Where(e => e.IsActiveNow()).ToList();
            var upcomingEvents = eventManager.Events.Where(e => !e.IsActiveNow()).ToList();

            var response = new StringBuilder();
            response.AppendLine();
            response.AppendLine("Active Events:");
            response.AppendLine();
            if (activeEvents.Any())
            {
                foreach (var eventItem in activeEvents)
                {
                    response.AppendLine($"{eventItem.EventName} - Start: {eventItem.StartTime:hh\\:mm\\:ss}, End: {eventItem.EndTime:hh\\:mm\\:ss}");
                }
            }
            else
            {
                response.AppendLine("No active events currently.");
            }

            response.AppendLine();
            response.AppendLine("Upcoming Events:");
            response.AppendLine();
            response.Append(GenerateUpcomingEventsScheduleText(eventManager, DateTime.Now));

            EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", response.ToString(), Color.Green, Context.Player.SteamUserId);
        }

        private string GenerateUpcomingEventsScheduleText(EventManager eventManager, DateTime now)
        {
            int currentMonth = now.Month;
            int currentYear = now.Year;
            var monthsToCheck = new List<(int year, int month)>
            {
                (currentYear, currentMonth),
                (currentMonth == 12 ? currentYear + 1 : currentYear, (currentMonth % 12) + 1),
                (currentMonth >= 11 ? currentYear + 1 : currentYear, (currentMonth + 1) % 12 + 1)
            };

            var upcomingEvents = new List<(DateTime start, DateTime end, string eventName)>();

            foreach (var eventItem in eventManager.Events)
            {
                foreach (var (year, month) in monthsToCheck)
                {
                    var nextEventDates = FindAllEventDatesInMonth(eventItem, year, month);
                    foreach (var nextEventDate in nextEventDates)
                    {
                        var nextStartDate = nextEventDate;
                        var nextEndDate = nextEventDate.Date.Add(eventItem.EndTime);
                        upcomingEvents.Add((nextStartDate, nextEndDate, eventItem.EventName));
                    }
                }
            }

            // Sort events and limit to the first 10
            var text = new StringBuilder();
            foreach (var eventInfo in upcomingEvents.OrderBy(e => e.start).Take(5))
            {
                text.AppendLine($"{eventInfo.eventName} - Start: {eventInfo.start:dd/MM/yyyy HH:mm:ss}, End: {eventInfo.end:dd/MM/yyyy HH:mm:ss}");
            }

            return text.ToString();
        }

        private IEnumerable<DateTime> FindAllEventDatesInMonth(EventsBase eventItem, int year, int month)
        {
            var dates = new List<DateTime>();
            foreach (var day in eventItem.ActiveDaysOfMonth.OrderBy(d => d))
            {
                var potentialNextDate = new DateTime(year, month, day,
                                                     eventItem.StartTime.Hours, eventItem.StartTime.Minutes, eventItem.StartTime.Seconds);
                if (potentialNextDate > DateTime.Now)
                {
                    dates.Add(potentialNextDate);
                }
            }
            return dates;
        }

        [Command("transfer", "Initiate a point transfer.")]
        [Permission(MyPromoteLevel.None)]
        public void InitiateTransfer(long points)
        {
            if (Context.Player == null)
            {
                Log.Error("This command can only be used by a player.");
                return;
            }

            long steamId = (long)Context.Player.SteamUserId;
            string transferCode = Plugin.PointsTransferManager.InitiateTransfer(steamId, points);
            EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Transfer initiated. Your transfer code: {transferCode}", Color.Green, Context.Player.SteamUserId);
        }

        [Command("claim", "Complete a point transfer using a transfer code.")]
        [Permission(MyPromoteLevel.None)]
        public async Task CompleteTransfer(string transferCode)
        {
            if (Context.Player == null)
            {
                Log.Error("This command can only be used by a player.");
                return;
            }

            long steamId = (long)Context.Player.SteamUserId;
            var (success, points) = await Plugin.PointsTransferManager.CompleteTransfer(transferCode, steamId);

            if (success)
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Transfer completed successfully. You received {points} points.", Color.Green, Context.Player.SteamUserId);
            }
            else
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "Transfer failed or code is invalid.", Color.Red, Context.Player.SteamUserId);
            }
        }

    }
}
