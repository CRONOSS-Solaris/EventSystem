using EventSystem.Events;
using EventSystem.Utils;
using NLog.Fluent;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRageMath;

namespace EventSystem
{
    [Category("Event")]
    public class PlayerEventSystemCommands : CommandModule
    {
        public EventSystemMain Plugin => (EventSystemMain)Context.Plugin;

        [Command("checkpoints", "Check your points.")]
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
                long points = Plugin.DatabaseManager.GetPlayerPoints(steamId);
                string response = new StringBuilder()
                    .AppendLine()
                    .AppendLine("---------------------")
                    .AppendLine("Available pts")
                    .AppendLine($"- {points}")
                    .AppendLine("---------------------")
                    .ToString();

                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", response, Color.Green, Context.Player.SteamUserId);
            }
            else
            {
                // Logika plików XML
                string fileName = $"{steamId}.xml";
                string playerFolder = Path.Combine(Plugin.StoragePath, "EventSystem", "PlayerAccounts");
                string filePath = Path.Combine(playerFolder, fileName);

                if (!File.Exists(filePath))
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "You do not have an account file.", Color.Red, Context.Player.SteamUserId);
                    return;
                }

                XmlSerializer serializer = new XmlSerializer(typeof(PlayerAccount));
                PlayerAccount playerAccount;
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                {
                    playerAccount = (PlayerAccount)serializer.Deserialize(fileStream);
                }

                string response = new StringBuilder()
                    .AppendLine()
                    .AppendLine("---------------------")
                    .AppendLine("Available pts")
                    .AppendLine($"- {playerAccount.Points}")
                    .AppendLine("---------------------")
                    .ToString();

                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", response, Color.Green, Context.Player.SteamUserId);
            }
        }

        [Command("showevents", "Displays active and upcoming events.")]
        [Permission(MyPromoteLevel.None)]
        public void ShowEvents()
        {
            var eventManager = Plugin._eventManager; // Pobierz menedżer eventów
            var activeEvents = eventManager.Events.Where(e => e.IsActiveNow()).ToList();
            var upcomingEvents = eventManager.Events.Where(e => !e.IsActiveNow()).ToList();

            var response = new StringBuilder();
            response.AppendLine("Active Events:");
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
            foreach (var eventItem in upcomingEvents)
            {
                var nextEventDate = FindNextEventDate(eventItem, DateTime.Now);
                if (nextEventDate.HasValue)
                {
                    var nextStartDate = nextEventDate.Value;
                    var nextEndDate = nextEventDate.Value.Date.Add(eventItem.EndTime);
                    response.AppendLine($"{eventItem.EventName} - Next Start: {nextStartDate:dd/MM/yyyy HH:mm:ss}, End: {nextEndDate:dd/MM/yyyy HH:mm:ss}");
                }
            }

            EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", response.ToString(), Color.Green, Context.Player.SteamUserId);
        }

        private DateTime? FindNextEventDate(EventsBase eventItem, DateTime now)
        {
            DateTime? nextEventDate = null;

            // Sprawdzanie dat w bieżącym miesiącu i następnym
            for (int monthOffset = 0; monthOffset <= 1; monthOffset++)
            {
                int year = (now.Month + monthOffset > 12) ? now.Year + 1 : now.Year;
                int month = (now.Month + monthOffset > 12) ? 1 : now.Month + monthOffset;

                foreach (var day in eventItem.ActiveDaysOfMonth.OrderBy(d => d))
                {
                    var potentialNextDate = new DateTime(year, month, day,
                                                         eventItem.StartTime.Hours, eventItem.StartTime.Minutes, eventItem.StartTime.Seconds);
                    if (potentialNextDate > now)
                    {
                        nextEventDate = potentialNextDate;
                        return nextEventDate;
                    }
                }
            }

            return nextEventDate;
        }
    }
}
