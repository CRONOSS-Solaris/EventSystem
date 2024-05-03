using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using EventSystem.Events;
using EventSystem.Managers;
using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Plugins;

namespace EventSystem.Discord.Commands
{
    public class DiscordSlashCommands : ApplicationCommandModule
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/DiscordSlashCommands");
        private DiscordBotConfig DBConfig => EventSystemMain.Instance.DiscordBotConfig;
        //private EventManager _EventManager => EventSystemMain.Instance._eventManager;

        [SlashCommand("checkpoints", "Check your points using your Discord ID.")]
        public async Task CheckPoints(InteractionContext ctx)
        {
            // Prevent non-guild interactions for user privacy
            if (ctx.Guild == null)
            {
                await ctx.CreateResponseAsync("This command can only be used within a server.", true);
                return;
            }

            string discordId = ctx.User.Id.ToString();

            long? pointsResult;
            if (EventSystemMain.Instance.Config.UseDatabase)
            {
                pointsResult = await EventSystemMain.Instance.DatabaseManager.GetPlayerPointsByDiscordId(discordId);
            }
            else
            {
                pointsResult = await EventSystemMain.Instance.PlayerAccountXmlManager.GetPlayerPointsByDiscordId(discordId);
            }

            if (pointsResult.HasValue)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "🌟 Points Check 🌟",
                    Description = $"**Hello {ctx.Member.DisplayName}!**\n\nYou currently have **{pointsResult.Value} points**. Keep participating to earn more!",
                    Color = DiscordColor.PhthaloGreen,
                    Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail { Url = ctx.User.AvatarUrl },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = "Event System Plugin",
                        IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                    },
                };
                await ctx.CreateResponseAsync(embed: embed, ephemeral: true);
            }
            else
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "⚠️ Account Not Found ⚠️",
                    Description = $"**Hey {ctx.Member.DisplayName},**\n\nNo points account found for your Discord ID. Please ensure you have registered by entering the server and using the command `!event discordlogin`.",
                    Color = DiscordColor.Red,
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = "Event System Plugin",
                        IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                    },
                };
                await ctx.CreateResponseAsync(embed: embed, ephemeral: true);  // Using ephemeral to only show this message to the user
            }
        }

        [SlashCommand("listrewards", "Displays available rewards.")]
        public async Task ListRewards(InteractionContext ctx)
        {
            if (ctx.Guild == null)
            {
                await ctx.CreateResponseAsync("This command can only be used within a server.", true);
                return;
            }

            string discordId = ctx.User.Id.ToString();
            long? playerPoints;

            if (EventSystemMain.Instance.Config.UseDatabase)
            {
                playerPoints = await EventSystemMain.Instance.DatabaseManager.GetPlayerPointsByDiscordId(discordId);
            }
            else
            {
                playerPoints = await EventSystemMain.Instance.PlayerAccountXmlManager.GetPlayerPointsByDiscordId(discordId);
            }

            var embed = new DiscordEmbedBuilder
            {
                Title = "🏆 Available Rewards 🏆",
                Description = playerPoints.HasValue ? $"You currently have **{playerPoints.Value} points**.\n\nHere are the rewards you can redeem:" : "Your points could not be retrieved or you have no points.",
                Color = DiscordColor.Gold,
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = "Event System Plugin",
                    IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                },
            };

            if (playerPoints.HasValue)
            {
                // Display pack rewards
                var packRewards = EventSystemMain.Instance.PackRewardsConfig.RewardSets;
                if (packRewards.Any())
                {
                    StringBuilder packRewardsText = new StringBuilder();
                    foreach (var rewardSet in packRewards)
                    {
                        packRewardsText.AppendLine($"**{rewardSet.Name} - {rewardSet.CostInPoints} PTS**");
                        foreach (var item in rewardSet.Items)
                        {
                            packRewardsText.AppendLine($"- {item.ItemSubtypeId} x{item.Amount} - Chance: {item.ChanceToDrop}%");
                        }
                    }
                    embed.AddField("📦 Pack Rewards", packRewardsText.ToString(), false);
                }
                else
                {
                    embed.AddField("📦 Pack Rewards", "No pack rewards available at this moment.", false);
                }

                // Display individual items
                var itemRewards = EventSystemMain.Instance.ItemRewardsConfig.IndividualItems;
                if (itemRewards.Any())
                {
                    StringBuilder individualItemsText = new StringBuilder();
                    foreach (var item in itemRewards)
                    {
                        individualItemsText.AppendLine($"- {item.ItemSubtypeId} x{item.Amount} - {item.CostInPoints} PTS");
                    }
                    embed.AddField("🛍️ Individual Rewards", individualItemsText.ToString(), false);
                }
                else
                {
                    embed.AddField("🛍️ Individual Rewards", "No individual rewards available at this moment.", false);
                }
            }

            await ctx.CreateResponseAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("events", "Displays active and upcoming events.")]
        public async Task ShowEvents(InteractionContext ctx)
        {
            if (ctx.Guild == null)
            {
                await ctx.CreateResponseAsync("This command can only be used within a server.", true);
                return;
            }

            await ctx.CreateResponseAsync(new DiscordEmbedBuilder
            {
                Title = "🔍 Fetching Events...",
                Description = "Please hold on while I retrieve the latest events for you. This won't take long.",
                Color = DiscordColor.Azure,
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = "Event System Plugin",
                    IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                },
            }, true); // Immediate feedback that command is being processed

            try
            {
                var eventManager = EventSystemMain.Instance._eventManager;
                var activeEvents = eventManager.Events.Where(e => e.IsActiveNow()).ToList();
                var upcomingEventsText = await GenerateUpcomingEventsScheduleText(eventManager, DateTime.UtcNow);

                var response = new StringBuilder();

                if (activeEvents.Count > 0)
                {
                    response.AppendLine("🔥 **Active Events:**");
                    foreach (var eventItem in activeEvents)
                    {
                        string endTime = $"{eventItem.EndTime:hh\\:mm\\:ss}"; // Format TimeSpan directly
                        response.AppendLine($"- **{eventItem.EventName}** Ends in: {endTime}\n👥 **Participants:** {eventItem.GetParticipantsCount()}\n📝 **Description:** {eventItem.EventDescription}\n");
                    }
                }
                else
                {
                    response.AppendLine("❌ No active events currently.");
                }

                if (!string.IsNullOrEmpty(upcomingEventsText))
                {
                    response.AppendLine("📅 **Upcoming Events:**");
                    response.AppendLine(upcomingEventsText);
                }
                else
                {
                    response.AppendLine("❌ No upcoming events.");
                }

                var webhookBuilder = new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                {
                    Title = "🔍 Events Overview",
                    Description = response.ToString(),
                    Color = DiscordColor.Azure,
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = "Event System Plugin",
                        IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                    }
                });

                await ctx.EditResponseAsync(webhookBuilder);
            }
            catch (Exception ex)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Failed to fetch events due to an error: " + ex.Message));
                Log.Error("Error fetching events: " + ex.ToString());
            }
        }

        private async Task<string> GenerateUpcomingEventsScheduleText(EventManager eventManager, DateTime now)
        {
            var monthsToCheck = new List<(int year, int month)>
            {
                (now.Year, now.Month),
                (now.Month == 12 ? now.Year + 1 : now.Year, (now.Month % 12) + 1),
                (now.Month >= 11 ? now.Year + 1 : now.Year, (now.Month + 1) % 12 + 1)
            };

            var upcomingEvents = new List<(DateTime start, DateTime end, string eventName)>();

            foreach (var eventItem in eventManager.Events)
            {
                if (!eventItem.IsEnabled) continue;

                foreach (var (year, month) in monthsToCheck)
                {
                    var nextEventDates = EventSystemMain.Instance._allEventsLcdManager.FindAllEventDatesInMonth(eventItem, year, month);
                    foreach (var nextEventDate in nextEventDates)
                    {
                        var nextStartDate = nextEventDate;
                        var nextEndDate = nextEventDate.Date.Add(eventItem.EndTime); // Assuming Duration is a TimeSpan
                        upcomingEvents.Add((nextStartDate, nextEndDate, eventItem.EventName));
                    }
                }
            }

            upcomingEvents = upcomingEvents.OrderBy(e => e.start).Take(3).ToList();

            var text = new StringBuilder();
            if (!upcomingEvents.Any())
            {
                return "";
            }

            foreach (var eventInfo in upcomingEvents)
            {
                text.AppendLine($"**{eventInfo.eventName}**\n- Start: {eventInfo.start:dd/MM/yyyy HH:mm:ss}\n- End: {eventInfo.end:dd/MM/yyyy HH:mm:ss}\n");
            }

            return text.ToString();
        }


        [SlashCommand("buyreward", "Purchase a reward using your points.")]
        public async Task BuyRewardCommand(InteractionContext ctx, [Option("reward_name", "Name of the reward to purchase")] string rewardName)
        {
            // Send an initial response that the purchase is being processed.
            await ctx.CreateResponseAsync(new DiscordEmbedBuilder
            {
                Title = "🛒 Processing Your Purchase...",
                Description = $"We're currently processing your purchase of **{rewardName}**. Please hold on for just a moment.",
                Color = DiscordColor.Orange,
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = "Event System Plugin",
                    IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                },
            }, true); // Set to true if the response should be ephemeral.

            try
            {
                if (ctx.Guild == null)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = "🚫 Error",
                        Description = "This command can only be used within a server.",
                        Color = DiscordColor.Red,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = "Event System Plugin",
                            IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                        },
                    }));
                    return;
                }

                if (string.IsNullOrWhiteSpace(rewardName))
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = "🚫 Invalid Reward Name",
                        Description = "It seems like the reward name specified does not exist. Please double-check the name and try again.",
                        Color = DiscordColor.Red,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = "Event System Plugin",
                            IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                        },
                    }));
                    return;
                }

                string discordId = ctx.User.Id.ToString();
                ulong? steamId = null;

                // Retrieve Steam ID using Discord ID
                if (EventSystemMain.Instance.Config.UseDatabase)
                {
                    var steamIdObject = await EventSystemMain.Instance.DatabaseManager.GetSteamIdByDiscordId(discordId);
                    if (steamIdObject == null)
                    {
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                        {
                            Title = "🔗 Account Not Linked",
                            Description = "No linked Steam account found. Please register with `!event discordlogin` to link your accounts.",
                            Color = DiscordColor.Red,
                            Footer = new DiscordEmbedBuilder.EmbedFooter
                            {
                                Text = "Event System Plugin",
                                IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                            },
                        }));
                        return;
                    }
                    steamId = (ulong?)steamIdObject;
                }
                else
                {
                    var steamIdObject = await EventSystemMain.Instance.PlayerAccountXmlManager.GetSteamIdByDiscordId(discordId);
                    if (steamIdObject == null)
                    {
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                        {
                            Title = "🔗 Account Not Linked",
                            Description = "No linked Steam account found. Please register with `!event discordlogin` to link your accounts.",
                            Color = DiscordColor.Red,
                            Footer = new DiscordEmbedBuilder.EmbedFooter
                            {
                                Text = "Event System Plugin",
                                IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                            },
                        }));
                        return;
                    }
                    steamId = (ulong?)steamIdObject;
                }

                long identityId = EventSystemMain.Instance.GetIdentityId((ulong)steamId.Value);
                if (identityId == 0)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = "🚫 Game Identity Not Found",
                        Description = "We were unable to locate a game identity associated with the provided Steam ID. This may be due to an unlinked or incorrectly linked Steam account. Please ensure your account is correctly linked and try again.",
                        Color = DiscordColor.Red,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = "Event System Plugin",
                            IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                        },
                    }));
                    return;
                }

                bool isPlayerOnline = EventSystemMain.Instance.IsPlayerOnline(identityId);
                if (!isPlayerOnline)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = "⏳ Player Not Online",
                        Description = "The player must be online on the server to purchase and receive rewards. Please connect to the server.",
                        Color = DiscordColor.Red,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = "Event System Plugin",
                            IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                        },
                    }));
                    return;
                }

                long? points = await GetPlayerPoints(ctx, discordId);
                if (points == null || points.Value <= 0)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = "💸 Insufficient Points",
                        Description = "You do not have enough points to purchase the desired rewards. Please accumulate more points and try again.",
                        Color = DiscordColor.Red,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = "Event System Plugin",
                            IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                        },
                    }));
                    return;
                }

                ulong steamIdUnsigned = unchecked((ulong)steamId.Value);
                bool success = await PurchaseReward(ctx, steamIdUnsigned, rewardName, points.Value);

                if (!success)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = "❌ Purchase Unsuccessful",
                        Description = "We encountered an issue while trying to process your purchase. Please ensure the reward name is correct and that you have sufficient points to complete the transaction. If you're unsure, you can check your points with `!points` command or review the available rewards list.",
                        Color = DiscordColor.Red,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = "Event System Plugin",
                            IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                        },
                    }));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred in BuyRewardCommand: {ex.Message}");
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                {
                    Title = "⚠️ Unexpected Error",
                    Description = "Oops! Something went wrong on our end. Please don't worry—we're on it! In the meantime, you can help by contacting support with a brief description of what you were trying to do. This will help us resolve the issue faster.",
                    Color = DiscordColor.Red,
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = "Event System Plugin",
                        IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                    },
                }));
            }
        }

        private async Task<long?> GetPlayerPoints(InteractionContext ctx, string discordId)
        {
            // Retrieve player points based on the system configuration
            if (EventSystemMain.Instance.Config.UseDatabase)
            {
                return await EventSystemMain.Instance.DatabaseManager.GetPlayerPointsByDiscordId(discordId);
            }
            else
            {
                return await EventSystemMain.Instance.PlayerAccountXmlManager.GetPlayerPointsByDiscordId(discordId);
            }
        }


        private async Task<bool> PurchaseReward(InteractionContext ctx, ulong steamId, string rewardName, long playerPoints)
        {
            Log.Info("Retrieving configuration for pack and item rewards...");
            var packRewardsConfig = EventSystemMain.Instance.PackRewardsConfig;
            var itemRewardsConfig = EventSystemMain.Instance.ItemRewardsConfig;

            var rewardSet = packRewardsConfig.RewardSets.FirstOrDefault(rs => rs.Name.Equals(rewardName, StringComparison.OrdinalIgnoreCase));
            var individualItem = itemRewardsConfig.IndividualItems.FirstOrDefault(ri => ri.ItemSubtypeId.Equals(rewardName, StringComparison.OrdinalIgnoreCase));

            long cost = rewardSet != null ? rewardSet.CostInPoints : individualItem?.CostInPoints ?? 0;

            if (cost > 0 && playerPoints >= cost)
            {
                StringBuilder itemsReceived = new StringBuilder("You received the following items:\n");
                bool anyItemsAwarded = false;

                if (rewardSet != null)
                {
                    foreach (var item in rewardSet.Items)
                    {
                        if (new Random().NextDouble() * 100 <= item.ChanceToDrop)
                        {
                            bool awarded = PlayerItemRewardManager.AwardPlayer(steamId, item, item.Amount, Log, EventSystemMain.Instance.Config);
                            if (awarded)
                            {
                                itemsReceived.AppendLine($"- {item.Amount}x {item.ItemSubtypeId} ({item.ItemTypeId})");
                                anyItemsAwarded = true;
                            }
                        }
                    }
                }
                else if (individualItem != null)
                {
                    bool awarded = PlayerItemRewardManager.AwardPlayer(steamId, individualItem, individualItem.Amount, Log, EventSystemMain.Instance.Config);
                    if (awarded)
                    {
                        itemsReceived.AppendLine($"- {individualItem.Amount}x {individualItem.ItemSubtypeId} ({individualItem.ItemTypeId})");
                        anyItemsAwarded = true;
                    }
                }

                if (anyItemsAwarded)
                {
                    bool updateResult = await UpdatePlayerPoints(steamId, -cost);
                    if (updateResult)
                    {
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                        {
                            Title = "🎉 Reward Successfully Awarded",
                            Description = itemsReceived.ToString(),
                            Color = DiscordColor.Green,
                            Footer = new DiscordEmbedBuilder.EmbedFooter
                            {
                                Text = "Event System Plugin",
                                IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                            },
                        }));
                        return true;
                    }
                    else
                    {
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                        {
                            Title = "❌ Failed to Update Points",
                            Description = "We encountered an issue updating your points after the items were awarded. Please contact an administrator.",
                            Color = DiscordColor.Red,
                            Footer = new DiscordEmbedBuilder.EmbedFooter
                            {
                                Text = "Event System Plugin",
                                IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                            },
                        }));
                        return false;
                    }
                }
                else
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = "🚫 Inventory Full",
                        Description = "We were unable to award any rewards because your inventory is full. No points have been deducted.",
                        Color = DiscordColor.Red,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = "Event System Plugin",
                            IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                        },
                    }));
                    return false;
                }
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                {
                    Title = "⚠️ Insufficient Points or Reward Not Found",
                    Description = "Either you do not have enough points to claim this reward, or the specified reward does not exist. Please verify and try again.",
                    Color = DiscordColor.Red,
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = "Event System Plugin",
                        IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                    },
                }));
                return false;
            }
        }


        private async Task<bool> UpdatePlayerPoints(ulong steamId, long pointsChange)
        {
            if (EventSystemMain.Instance.Config.UseDatabase)
            {
                return await EventSystemMain.Instance.DatabaseManager.UpdatePlayerPointsAsync(steamId.ToString(), pointsChange);
            }
            else
            {
                return await EventSystemMain.Instance.PlayerAccountXmlManager.UpdatePlayerPointsAsync((long)steamId, pointsChange);
            }
        }

        [SlashCommand("toppoints", "Displays the top 5 users with the most points.")]
        public async Task TopPoints(InteractionContext ctx)
        {
            if (ctx.Guild == null)
            {
                await ctx.CreateResponseAsync("This command can only be used within a server.", true);
                return;
            }

            // Fetch top users with points
            var topUsers = await GetTopUsersWithPoints();

            var embed = new DiscordEmbedBuilder
            {
                Title = "🏆 Top Points Leaders 🏆",
                Description = "Here are the top 5 players with the highest points. Keep competing to climb up the ranks!",
                Color = DiscordColor.Gold,
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = "Event System Plugin",
                    IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                },
            };

            // Add leaderboard fields
            if (topUsers.Any())
            {
                int rank = 1;
                foreach (var (Username, Points) in topUsers)
                {
                    embed.AddField($"Rank {rank}: {Username}", $"**{Points} Points** :star:", true);
                    rank++;
                }
            }
            else
            {
                embed.AddField("Leaders", "No data available or no players have points yet.", false);
            }

            await ctx.CreateResponseAsync(embed: embed);
        }

        private async Task<List<(string Username, int Points)>> GetTopUsersWithPoints()
        {
            if (EventSystemMain.Instance.Config.UseDatabase)
            {
                return await EventSystemMain.Instance.DatabaseManager.GetTopFiveUsersWithPoints();
            }
            else
            {
                return await EventSystemMain.Instance.PlayerAccountXmlManager.GetTopFiveUsersWithPoints();
            }
        }

        [SlashCommand("servercommands", "Shows available commands on the server")]
        public async Task ShowCommands(InteractionContext ctx)
        {
            if (ctx.Guild == null)
            {
                await ctx.CreateResponseAsync("This command can only be used within a server.", true);
                return;
            }

            var embed = new DiscordEmbedBuilder
            {
                Title = "📜 Available Commands",
                Description = "Here are the commands you can use on this server:",
                Color = DiscordColor.Blurple,
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = "Event System Plugin",
                    IconUrl = "https://torchapi.com/img/2bed6cf7-f299-4400-9d14-85fd6042fc43.png",
                },
            };

            embed.AddField("👤 Player Commands",
                "**!event help** - Shows help for commands.\n" +
                "**!event points** - Check your points.\n" +
                "**!event events** - Displays active and upcoming events.\n" +
                "**!event transfer [amount]** - Transfer points to another player.\n" +
                "**!event claim [code]** - Claim a transfer using the provided code.\n" +
                "**!event join [event name]** - Join an active event.\n" +
                "**!event leave [event name]** - Opt out of a specified event.\n" +
                "**!event buy [reward name]** - Purchase rewards using your points.\n" +
                "**!event listrewards** - View all available rewards.\n" +
                "**!event discordlogin** - Link your Discord to your game account.");

            embed.AddField("🛠️ Admin Commands",
                "**!eventAdmin modifypoints [SteamId] [amount]** - Adjust points for a specific player.\n" +
                "**!eventAdmin refreshblocks** - Update the list of interactable screens.\n" +
                "**!eventAdmin spawngird [FileName] [X] [Y] [Z]** - Deploy a grid at specified coordinates.\n" +
                "**!eventAdmin listnpcs** - Enumerate all NPCs currently active on the server.");

            await ctx.CreateResponseAsync(embed: embed.Build());
        }

    }
}