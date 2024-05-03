using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using EventSystem.Discord.Commands;
using EventSystem.Discord.Utils;
using EventSystem.Utils;
using NLog;
using System;
using System.Threading.Tasks;

namespace EventSystem.Discord
{
    public sealed class Bot
    {
        public DiscordClient Client { get; private set; }
        public InteractivityExtension Interactivity { get; private set; }
        private CommandsNextExtension Commands { get; set; }
        public MessageService MessageService { get; set; }
        private Logger Log = LogManager.GetLogger("EventSystemMain/DiscordBotMain");
        private Bot_Subscriptions m_subscriptions = new Bot_Subscriptions();
        public ServerData ServerData = new ServerData();
        public DiscordUser BotUser { get; set; }
        public readonly SendDM DMSender = new SendDM();
        public bool IsConnected { get; set; }
        private bool Inited;

        public async Task ConnectAsync()
        {
            if (string.IsNullOrWhiteSpace(EventSystemMain.Instance.DiscordBotConfig.Token))
            {
                await LoggerHelper.ErrorAsync(Log, "Invalid Bot Token, please set the token in the settings tab.");
                return;
            }

            if (!Inited)
            {
                if (!await InitAsync()) return; // Init failed, dont proceed.
                EventSystemMain.Instance.DiscordBotConfig.BotStatus = "Connecting...";
                await LoggerHelper.InfoAsync(Log, "Connecting...");
                await Client.ConnectAsync();
                return;
            }

            EventSystemMain.Instance.DiscordBotConfig.BotStatus = "Connecting...";
            await LoggerHelper.InfoAsync(Log, "Connecting...");
            await Client.ConnectAsync(); // Already Init'd, just connect.
        }

        private Task<bool> InitAsync()
        {
            if (string.IsNullOrWhiteSpace(EventSystemMain.Instance.DiscordBotConfig.Token))
            {
                LoggerHelper.ErrorAsync(Log, "Invalid Bot Token, please set the token in the settings tab.");
                return Task.FromResult(false);
            }

            Log.Info("Valid Token, Initing...");
            // Setup the client configuration
            DiscordConfiguration d_config = new DiscordConfiguration()
            {
                Token = EventSystemMain.Instance.DiscordBotConfig.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                Intents = DiscordIntents.All,
                HttpTimeout = TimeSpan.FromSeconds(30),
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Trace,
                LoggerFactory = new DSharpPlusNLogAdapter(LogLevel.Trace),
                GatewayCompressionLevel = GatewayCompressionLevel.None
            };

            Client = new DiscordClient(d_config);

            // Setup the interactivity module
            Client.UseInteractivity(new InteractivityConfiguration()
            {
                Timeout = TimeSpan.FromMinutes(1)
            });

            // Setup the commands module
            CommandsNextConfiguration c_config = new CommandsNextConfiguration()
            {
                StringPrefixes = EventSystemMain.Instance.DiscordBotConfig.Prefixes,
                EnableDms = true,
                EnableDefaultHelp = false,
                EnableMentionPrefix = true
            };

            Commands = Client.UseCommandsNext(c_config);
            SlashCommandsExtension slashCommandsConfig = Client.UseSlashCommands();

            // Prefix Commands
            // I may add some later, but for now, I don't think I need them.

            // Slash Commands
            if (ulong.TryParse(EventSystemMain.Instance.DiscordBotConfig.DiscordServerId, out ulong serverId))
            {
                if (serverId != 0)
                {
                    slashCommandsConfig.RegisterCommands<DiscordSlashCommands>(serverId);
                    LoggerHelper.InfoAsync(Log, $"Registered Slash Commands for Server ID [{serverId}]");
                }

                else
                    slashCommandsConfig.RegisterCommands<DiscordSlashCommands>();
            }
            else slashCommandsConfig.RegisterCommands<DiscordSlashCommands>();

            Commands.CommandErrored += Commands_CommandErrored;

            Client.Ready += m_subscriptions.Client_Ready;
            Client.GuildDownloadCompleted += m_subscriptions.Client_GuildDownloadCompleted;

            MessageService = new MessageService(this);

            Inited = true;
            return Task.FromResult(true);
        }

        private async Task Commands_CommandErrored(CommandsNextExtension sender, CommandErrorEventArgs args)
        {
            await LoggerHelper.ErrorAsync(Log, $"Command Errored: {args.Exception}");
        }

        public void Dispose()
        {
            Client?.Dispose();
            Interactivity?.Dispose();
            Commands?.Dispose();
        }
    }
}
