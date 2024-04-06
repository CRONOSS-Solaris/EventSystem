using EventSystem.Events;
using EventSystem.Managers;
using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;
using VRageMath;

namespace EventSystem
{
    [Category("event")]
    public class PlayerEventSystemCommands : CommandModule
    {
        public EventSystemMain Plugin => (EventSystemMain)Context.Plugin;
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/PlayerEventSystemCommands");

        [Command("help", "Shows help for commands available to you.")]
        [Permission(MyPromoteLevel.None)]
        public void Help()
        {
            var commandManager = Context.Torch.CurrentSession?.Managers.GetManager<CommandManager>();
            if (commandManager == null)
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "Must have an attached session to list commands", Color.Red, Context.Player.SteamUserId);
                return;
            }

            StringBuilder sb = new StringBuilder();
            var playerPromoteLevel = Context.Player?.PromoteLevel ?? MyPromoteLevel.None;

            // Iterate over all commands and add them to the StringBuilder if they are from this plugin and the player has the required permissions.
            foreach (CommandTree.CommandNode command in commandManager.Commands.WalkTree())
            {
                if (command.IsCommand && command.Command.Plugin == this.Plugin && command.Command.MinimumPromoteLevel <= playerPromoteLevel)
                {
                    sb.AppendLine($"{command.Command.SyntaxHelp}\n    {command.Command.HelpText}\n");
                }
            }

            // Check if there were any commands to list, then send them using ModCommunication to display as MOTD in dialog.
            if (sb.Length > 0)
            {
                string message = sb.ToString().TrimEnd();
                var dialogMessage = new DialogMessage("Event System Help", "Available commands:", message);
                ModCommunication.SendMessageTo(dialogMessage, Context.Player.SteamUserId);
            }
            else
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "No commands available for your permission level.", Color.Red, Context.Player.SteamUserId);
            }
        }

        [Command("points", "Check your points.")]
        [Permission(MyPromoteLevel.None)]
        public async Task CheckPoints()
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
                var pointsResult = await Plugin.DatabaseManager.GetPlayerPointsAsync(steamId);
                if (pointsResult.HasValue)
                {
                    string response = new StringBuilder()
                        .AppendLine()
                        .AppendLine("---------------------")
                        .AppendLine("Available pts")
                        .AppendLine($"- {pointsResult.Value} PTS")
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
                var playerAccount = await Plugin.PlayerAccountXmlManager.GetPlayerAccountAsync(steamId);
                if (playerAccount != null)
                {
                    string response = new StringBuilder()
                        .AppendLine()
                        .AppendLine("---------------------")
                        .AppendLine("Available pts")
                        .AppendLine($"- {playerAccount.Points} PTS")
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

        [Command("buy", "Buy rewards with points")]
        [Permission(MyPromoteLevel.None)]
        public async Task BuyReward(string rewardName = "")
        {
            if (Context.Player == null)
            {
                Log.Error("This command can only be used by a player.");
                return;
            }

            var cooldownManager = CommandInCooldown.Instance;
            var cooldown = TimeSpan.FromSeconds(10);

            if (cooldownManager.IsCommandInCooldown(Context.Player.SteamUserId, cooldown))
            {
                var timeLeft = cooldownManager.TimeLeftForCommand(Context.Player.SteamUserId, cooldown);
                var minutes = timeLeft.Minutes;
                var seconds = timeLeft.Seconds;

                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Wait {minutes}m {seconds}s to use this command again.", Color.Red, Context.Player.SteamUserId);
                return;
            }

            // Aktualizuj czas ostatniego użycia komendy
            cooldownManager.UpdateCommandUsage(Context.Player.SteamUserId);

            var steamId = Context.Player.SteamUserId;

            // Jeśli nie podano nazwy nagrody, wyświetl dostępne nagrody
            if (string.IsNullOrEmpty(rewardName))
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "Please specify the reward name. Use !event listrewards to view available rewards.", Color.Red, Context.Player.SteamUserId);
                return;
            }

            // Sprawdź, czy gracz ma wystarczającą ilość punktów
            var playerPoints = await GetPlayerPoints(steamId);
            if (!playerPoints.HasValue)
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Error retrieving your points. Please try again.", Color.Red, Context.Player.SteamUserId);
                return;
            }

            // Spróbuj kupić nagrodę
            await PurchaseReward(steamId, rewardName, playerPoints.Value);
        }

        [Command("listrewards", "Displays available rewards")]
        [Permission(MyPromoteLevel.None)]
        public void ListRewards()
        {
            if (Context.Player == null)
            {
                Log.Error("This command can only be used by a player.");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Available Rewards:\n");

            // Sekcja dla paczek nagród
            var packRewards = Plugin.PackRewardsConfig.RewardSets;
            if (packRewards.Any())
            {
                sb.AppendLine("Pack Rewards:\n");
                foreach (var rewardSet in packRewards)
                {
                    sb.AppendLine($"  {rewardSet.Name} - {rewardSet.CostInPoints} PTS");
                    foreach (var item in rewardSet.Items)
                    {
                        sb.AppendLine($"    • {item.ItemSubtypeId} x{item.Amount} - Chance: {item.ChanceToDrop}%");
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("  No Pack Rewards Available\n");
            }

            // Sekcja dla indywidualnych nagród
            var itemRewards = Plugin.ItemRewardsConfig.IndividualItems;
            if (itemRewards.Any())
            {
                sb.AppendLine("Individual Rewards:\n");
                foreach (var item in itemRewards)
                {
                    sb.AppendLine($"  • {item.ItemSubtypeId} x{item.Amount} - {item.CostInPoints} PTS");
                }
            }
            else
            {
                sb.AppendLine("  No Individual Rewards Available\n");
            }

            // Wyświetl nagrody w oknie MOTD
            var dialogMessage = new DialogMessage("Available Rewards", "You can buy these rewards with your points:", sb.ToString().TrimEnd());
            ModCommunication.SendMessageTo(dialogMessage, Context.Player.SteamUserId);
        }

        [Command("transfer", "Initiate a point transfer.")]
        [Permission(MyPromoteLevel.None)]
        public async Task InitiateTransfer(long points)
        {
            if (Context.Player == null)
            {
                Log.Error("This command can only be used by a player.");
                return;
            }

            long steamId = (long)Context.Player.SteamUserId;
            string transferCode = await Plugin.PointsTransferManager.InitiateTransfer(steamId, points);

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
        public async Task JoinEvent(string eventName)
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
                var (success, message) = await eventToJoin.AddPlayer(steamId);
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", message, success ? Color.Green : Color.Red, Context.Player.SteamUserId);
            }
            else
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Event '{eventName}' is not active or does not exist.", Color.Red, Context.Player.SteamUserId);
            }
        }

        // Komenda do opuszczania eventu z podaniem nazwy
        [Command("leave", "Leave a specified event.")]
        [Permission(MyPromoteLevel.None)]
        public async Task LeaveEvent(string eventName)
        {
            if (Context.Player == null)
            {
                Log.Error("This command can only be used by a player.");
                return;
            }

            long steamId = (long)Context.Player.SteamUserId;
            var eventManager = Plugin._eventManager;
            var eventToLeave = eventManager.Events.FirstOrDefault(e => e.EventName.Equals(eventName, StringComparison.OrdinalIgnoreCase));

            if (eventToLeave != null && eventToLeave.IsPlayerParticipating(steamId).Result)
            {
                var (success, message) = await eventToLeave.LeavePlayer(steamId);
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", message, success ? Color.Green : Color.Red, Context.Player.SteamUserId);
            }
            else
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"You are not participating in the event '{eventName}' or it does not exist.", Color.Red, Context.Player.SteamUserId);
            }
        }

        private async Task<long?> GetPlayerPoints(ulong steamId)
        {
            // Implementacja pobierania punktów gracza
            if (Plugin.Config.UseDatabase)
            {
                return await Plugin.DatabaseManager.GetPlayerPointsAsync((long)steamId);
            }
            else
            {
                var account = await Plugin.PlayerAccountXmlManager.GetPlayerAccountAsync((long)steamId);
                return account?.Points;
            }
        }

        private async Task PurchaseReward(ulong steamId, string rewardName, long playerPoints)
        {
            var packRewardsConfig = Plugin.PackRewardsConfig;
            var itemRewardsConfig = Plugin.ItemRewardsConfig;
            var rewardSet = packRewardsConfig.RewardSets.FirstOrDefault(rs => rs.Name.Equals(rewardName, StringComparison.OrdinalIgnoreCase));
            var individualItem = itemRewardsConfig.IndividualItems.FirstOrDefault(ri => ri.ItemSubtypeId.Equals(rewardName, StringComparison.OrdinalIgnoreCase));

            long cost = rewardSet != null ? rewardSet.CostInPoints : individualItem?.CostInPoints ?? 0;

            if (cost > 0 && playerPoints >= cost)
            {
                StringBuilder itemsReceived = new StringBuilder("You received:\n");
                bool anyItemsAwarded = false; // Zakładamy, że na początku żadne przedmioty nie zostały przyznane

                if (rewardSet != null)
                {
                    // Próbuj przyznać każdy przedmiot z zestawu
                    foreach (var item in rewardSet.Items)
                    {
                        if (new Random().NextDouble() * 100 <= item.ChanceToDrop)
                        {
                            bool awarded = PlayerItemRewardManager.AwardPlayer(steamId, item, item.Amount, Log, Plugin.Config);
                            if (awarded)
                            {
                                itemsReceived.AppendLine($"- {item.Amount}x {item.ItemSubtypeId}");
                                anyItemsAwarded = true;
                            }
                        }
                    }
                }
                else if (individualItem != null)
                {
                    // Przyznaj indywidualny przedmiot
                    bool awarded = PlayerItemRewardManager.AwardPlayer(steamId, individualItem, individualItem.Amount, Log, Plugin.Config);
                    if (awarded)
                    {
                        itemsReceived.AppendLine($"- {individualItem.Amount}x {individualItem.ItemSubtypeId}");
                        anyItemsAwarded = true;
                    }
                }

                if (anyItemsAwarded)
                {
                    // Odejmij punkty tylko, jeśli jakieś przedmioty zostały przyznane
                    bool updateResult = await UpdatePlayerPoints(steamId, -cost);
                    if (updateResult)
                    {
                        EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", itemsReceived.ToString(), Color.Green, steamId);
                    }
                    else
                    {
                        EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "Failed to update points after awarding items. Please contact an admin.", Color.Red, steamId);
                    }
                }
                else
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "It was not possible to award rewards because your inventory is full. Points were not deducted.", Color.Red, steamId);

                }
            }
            else
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "You do not have enough points for this reward or it does not exist.", Color.Red, steamId);
            }
        }


        private async Task<bool> UpdatePlayerPoints(ulong steamId, long pointsChange)
        {
            if (Plugin.Config.UseDatabase)
            {
                return await Plugin.DatabaseManager.UpdatePlayerPointsAsync(steamId.ToString(), pointsChange);
            }
            else
            {
                return await Plugin.PlayerAccountXmlManager.UpdatePlayerPointsAsync((long)steamId, pointsChange);
            }
        }

    }
}
