using DSharpPlus.Entities;
using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSystem.Discord.Utils
{
    public class MessageService
    {
        private Logger Log = LogManager.GetLogger("EventSystemMain/MessageService");
        private Bot _bot;

        public MessageService(Bot bot)
        {
            _bot = bot;
        }

        // Method to retrieve all registered Discord IDs
        public async Task<IEnumerable<ulong>> GetAllRegisteredDiscordIds()
        {
            Log.Info("Fetching all registered Discord IDs.");
            List<ulong> discordIds = new List<ulong>();

            if (EventSystemMain.Instance.Config.UseDatabase)
            {
                discordIds.AddRange(await EventSystemMain.Instance.DatabaseManager.GetAllDiscordIds());
            }
            else
            {
                discordIds.AddRange(await EventSystemMain.Instance.PlayerAccountXmlManager.GetAllDiscordIds());
            }

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Fetched IDs: {string.Join(", ", discordIds)}");
            return discordIds;
        }

        // Method to send embed messages to all registered users
        public async Task SendEmbedMessageToAllRegisteredUsers(string title, string description, DiscordColor color)
        {
            Log.Info("Sending embedded messages to all registered users.");
            var discordIds = await GetAllRegisteredDiscordIds();
            int messageCount = 0;

            foreach (ulong discordId in discordIds)
            {
                DiscordMember member = null;
                try
                {
                    member = await _bot.ServerData.guild.GetMemberAsync(discordId);
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to retrieve member with ID {discordId}: {ex.Message}");
                    continue;
                }

                if (member != null)
                {
                    var embed = new DiscordEmbedBuilder
                    {
                        Title = title,
                        Description = description,
                        Color = color,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = "Event System Plugin",
                            IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                        },
                    }.Build();

                    try
                    {
                        await member.SendMessageAsync(embed: embed);
                        Log.Debug($"Message sent to {member.DisplayName} ({member.Id}).");
                        messageCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to send message to {member.DisplayName} ({member.Id}): {ex.Message}");
                    }
                }
                else
                {
                    Log.Warn($"Could not find a Discord member with ID {discordId}. Message not sent.");
                }
            }

            Log.Info($"Total of {messageCount} messages sent.");
        }
    }
}
