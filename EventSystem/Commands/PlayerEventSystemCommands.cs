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
            response.AppendLine("Active Events:");

            if (activeEvents.Any())
            {
                foreach (var eventItem in activeEvents)
                {
                    int participantsCount = eventItem.GetParticipantsCount();
                    response.AppendLine($"{eventItem.EventName} - End: {eventItem.EndTime:hh\\:mm\\:ss} - Participants: {participantsCount}");
                }
            }
            else
            {
                response.AppendLine("No active events currently.");
            }

            response.AppendLine();
            response.AppendLine("Upcoming Events:");

            var upcomingEventsText = GenerateUpcomingEventsScheduleText(eventManager, DateTime.Now);
            if (string.IsNullOrEmpty(upcomingEventsText))
            {
                response.AppendLine("No upcoming events.");
            }
            else
            {
                response.Append(upcomingEventsText);
            }

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
                // Pomiń wydarzenia, które są wyłączone
                if (!eventItem.IsEnabled) continue;

                foreach (var (year, month) in monthsToCheck)
                {
                    
                    var nextEventDates = Plugin._allEventsLcdManager.FindAllEventDatesInMonth(eventItem, year, month);
                    foreach (var nextEventDate in nextEventDates)
                    {
                        var nextStartDate = nextEventDate;
                        var nextEndDate = nextEventDate.Date.Add(eventItem.EndTime);
                        upcomingEvents.Add((nextStartDate, nextEndDate, eventItem.EventName));
                    }
                }
            }

            // Sortuj wydarzenia i ogranicz do pierwszych 10
            var text = new StringBuilder();
            if (!upcomingEvents.Any())
            {
                return "";
            }

            foreach (var eventInfo in upcomingEvents.OrderBy(e => e.start).Take(1))
            {
                text.AppendLine($"{eventInfo.eventName}\n- Start: {eventInfo.start:dd/MM/yyyy HH:mm:ss}\n- End: {eventInfo.end:dd/MM/yyyy HH:mm:ss}");
            }

            return text.ToString();
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

            if (transferCode != null)
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Transfer initiated. Your transfer code: {transferCode}", Color.Green, Context.Player.SteamUserId);
            }
            else
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "Transfer failed. Insufficient points.", Color.Red, Context.Player.SteamUserId);
            }
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
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "Transfer failed, code is invalid or sender does not have enough points.", Color.Red, Context.Player.SteamUserId);
            }
        }

        // Komenda do dołączania do eventu
        [Command("join", "Join an active event.")]
        [Permission(MyPromoteLevel.None)]
        public void JoinEvent(string eventName)
        {
            if (Context.Player == null)
            {
                Log.Error("This command can only be used by a player.");
                return;
            }

            long steamId = (long)Context.Player.SteamUserId;
            var eventManager = Plugin._eventManager;
            var eventToJoin = eventManager.Events.FirstOrDefault(e => e.EventName.Equals(eventName, StringComparison.OrdinalIgnoreCase) && e.IsActiveNow());

            if (eventToJoin != null)
            {
                // Sprawdź, czy gracz uczestniczy już w innym evencie, który nie zezwala na uczestnictwo w innych eventach
                if (eventManager.Events.Any(e => e != eventToJoin && e.IsActiveNow() && e.IsPlayerParticipating(steamId).Result && !e.AllowParticipationInOtherEvents))
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "You cannot join this event because you are participating in another event that does not allow multiple event participation.", Color.Red, Context.Player.SteamUserId);
                    return;
                }

                Task.Run(async () =>
                {
                    await eventToJoin.AddPlayer(steamId);
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"You have joined the event: {eventName}", Color.Green, Context.Player.SteamUserId);
                });
            }
            else
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Event '{eventName}' is not active or does not exist.", Color.Red, Context.Player.SteamUserId);
            }
        }

        // Komenda do opuszczania eventu
        [Command("leave", "Leave the current event.")]
        [Permission(MyPromoteLevel.None)]
        public void LeaveEvent()
        {
            if (Context.Player == null)
            {
                Log.Error("This command can only be used by a player.");
                return;
            }

            long steamId = (long)Context.Player.SteamUserId;
            var eventManager = Plugin._eventManager;
            var activeEvent = eventManager.Events.FirstOrDefault(e => e.IsPlayerParticipating(steamId).Result && e.IsActiveNow());

            if (activeEvent != null)
            {
                Task.Run(async () =>
                {
                    await activeEvent.RemovePlayer(steamId);
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"You have left the event: {activeEvent.EventName}", Color.Green, Context.Player.SteamUserId);
                });
            }
            else
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "You are not participating in any active event.", Color.Red, Context.Player.SteamUserId);
            }
        }

    }
}
