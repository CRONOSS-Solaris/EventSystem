using DSharpPlus.Entities;
using EventSystem.Config;
using EventSystem.DataBase;
using EventSystem.Discord;
using EventSystem.Discord.Utils;
using EventSystem.Discord.Web;
using EventSystem.Events;
using EventSystem.Managers;
using EventSystem.Nexus;
using EventSystem.Serialization;
using EventSystem.Utils;
using Nexus.API;
using NLog;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Managers;
using Torch.Session;
//#nullable enable

namespace EventSystem
{
    public partial class EventSystemMain : TorchPluginBase, IWpfPlugin
    {

        public static readonly Logger Log = LogManager.GetLogger("EventSystemMain");
        public static EventSystemMain Instance;

        public static bool CompileFailed { get; set; } = false;
        public static List<Assembly> myAssemblies = new List<Assembly>();
        public static string path;
        public static string basePath;
        public const string PluginName = "EventSystem";

        // Manager odpowiedzialny za multiplayer
        private IMultiplayerManagerBase _multiplayerManager;

        // Dostęp do ChatManager, aby wysyłać wiadomości do graczy
        public static IChatManagerServer ChatManager => TorchBase.Instance.CurrentSession.Managers.GetManager<IChatManagerServer>();

        // GUI
        private EventSystemControl _control;
        public UserControl GetControl() => _control ?? (_control = new EventSystemControl(this));

        // Konfiguracja
        private Persistent<EventSystemConfig> _config;
        public EventSystemConfig Config => _config?.Data;
        private Persistent<DiscordBotConfig> _discordBotConfig;
        public DiscordBotConfig DiscordBotConfig => _discordBotConfig?.Data;
        private Persistent<PackRewardsConfig> _packRewardsConfig;
        public PackRewardsConfig PackRewardsConfig => _packRewardsConfig?.Data;
        private Persistent<ItemRewardsConfig> _itemRewardsConfig;
        public ItemRewardsConfig ItemRewardsConfig => _itemRewardsConfig?.Data;

        // Integracja z Nexus
        public static NexusAPI? nexusAPI { get; private set; }
        private static readonly Guid NexusGUID = new("28a12184-0422-43ba-a6e6-2e228611cca5");
        public static bool NexusInstalled { get; private set; } = false;
        public static bool NexusInited;

        // PostgresSQL
        private PostgresDatabaseManager _databaseManager;
        public PostgresDatabaseManager DatabaseManager => _databaseManager;

        // PlayerAccountXmlManager
        private PlayerAccountXmlManager _playerAccountXmlManager;
        public PlayerAccountXmlManager PlayerAccountXmlManager => _playerAccountXmlManager;

        // PointsTransferManager
        private PointsTransferManager _pointsTransferManager;
        public PointsTransferManager PointsTransferManager => _pointsTransferManager;

        // Zarządzanie eventami
        public EventManager _eventManager;

        // Zarządzanie LCD
        public ActiveEventsLCDManager _activeEventsLCDManager;
        public AllEventsLCDManager _allEventsLcdManager;

        //GridSpawner
        private GridSpawner _gridSpawner;

        //DiscordBot
        public static Bot DiscordBot = new Bot();
        public bool WorldOnline;
        private DiscordHttpServer _discordHttpServer;
        public MessageService MessageService { get; private set; }


        //EventsBase

        public EventsBase EventsBase;

        //UpdateManager
        public Utils.UpdateManager UpdateManager { get; private set; }

        //listy do przechowywania dostępnych typów i podtypów przedmiotów
        public List<string> AvailableItemTypes { get; private set; } = new List<string>();
        public Dictionary<string, List<string>> AvailableItemSubtypes { get; private set; } = new Dictionary<string, List<string>>();

        //Metody
        public override async void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;
            //config
            var fileManager = new FileManager(Path.Combine(StoragePath, "EventSystem"));
            _config = fileManager.SetupConfig("EventSystemConfig.cfg", new EventSystemConfig());
            _discordBotConfig = fileManager.SetupConfig("DiscordBotConfig.cfg", new DiscordBotConfig());
            _packRewardsConfig = fileManager.SetupConfig("PackRewardsConfig.cfg", new PackRewardsConfig());
            _itemRewardsConfig = fileManager.SetupConfig("ItemRewardsConfig.cfg", new ItemRewardsConfig());
            string jsonFilePath = Path.Combine(StoragePath, "EventSystem", "Config", "EntityIDs.json");
            fileManager.CreateFile(jsonFilePath);
            _packRewardsConfig.Data.GenerateExampleRewards();
            _itemRewardsConfig.Data.GenerateExampleIndividualItems();

            //PostgresSQL
            if (_config.Data.UseDatabase)
            {
                // Ciąg połączenia z ustawień konfiguracji
                string connectionString = $"Host={_config.Data.DatabaseHost};Port={_config.Data.DatabasePort};Username={_config.Data.DatabaseUsername};Password={_config.Data.DatabasePassword};Database={_config.Data.DatabaseName};";
                _databaseManager = new PostgresDatabaseManager(connectionString);
                _databaseManager.InitializeDatabase();
            }

            // Inicjalizacja menedżera kont XML
            _playerAccountXmlManager = new PlayerAccountXmlManager(StoragePath);
            // Inicjalizacja PointsTransferManager
            _pointsTransferManager = new PointsTransferManager();

            //DiscordBot
            DiscordBotConfig.BotStatus = "Offline";

            if (DiscordBotConfig.EnableDiscordBot)
            {
                await DiscordBot.ConnectAsync();
                MessageService = DiscordBot.MessageService;
            }

            // Events
            _eventManager = new EventManager(_config?.Data, _activeEventsLCDManager, _allEventsLcdManager, MessageService);
            // Automatyczna rejestracja eventów
            RegisterAllEvents();

            //inne
            var sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            if (sessionManager != null)
                sessionManager.SessionStateChanged += SessionChanged;
            else
                Log.Warn("No session manager loaded!");

            Save();

        }

        private async void SessionChanged(ITorchSession session, TorchSessionState state)
        {

            switch (state)
            {
                case TorchSessionState.Loading:
                    break;

                case TorchSessionState.Loaded:
                    //Nexus
                    ConnectNexus();

                    InitializeManagers(session);

                    //lcd
                    if (_config.Data.EnableActiveEventsLCDManager)
                    {
                        _activeEventsLCDManager = new ActiveEventsLCDManager(_eventManager, _config.Data);
                    }

                    if (_config.Data.EnableAllEventsLCDManager)
                    {
                        _allEventsLcdManager = new AllEventsLCDManager(_eventManager, _config.Data);
                    }


                    ExtractAndProcessDLLFiles();
                    //compiler
                    CompileAndLoadSourceCode(session);

                    // Events
                    _eventManager = new EventManager(_config?.Data, _activeEventsLCDManager, _allEventsLcdManager, MessageService);
                    // Automatyczna rejestracja eventów
                    RegisterAllEvents();

                    // Planowanie eventów po załadowaniu sesji
                    ScheduleAllEvents();

                    //UpdateManager start
                    UpdateManager = new Utils.UpdateManager();
                    UpdateManager.StartTimers();

                    //GridSpawner
                    _gridSpawner = new GridSpawner();

                    //item loader and button on
                    ItemLoader.LoadAvailableItemTypesAndSubtypes(AvailableItemTypes, AvailableItemSubtypes, Log, Config);
                    _control.Dispatcher.Invoke(() => _control.UpdateButtonState(true));

                    Log.Info("Session Loaded!");
                    WorldOnline = true;

                    //DiscordBot
                    if (DiscordBotConfig.EnableDiscordBot)
                    {
                        if (DiscordBot.IsConnected)
                            await DiscordBot.Client.UpdateStatusAsync(new DiscordActivity(Instance.DiscordBotConfig.StatusMessage, ActivityType.Playing), UserStatus.Online);
                        else
                        {
                            await DiscordBot.ConnectAsync();
                        }

                        _discordHttpServer = new DiscordHttpServer();
                        _discordHttpServer.Start($"http://{DiscordBotConfig.DiscordHttpAdress}/auth/");
                    }

                    break;

                case TorchSessionState.Unloading:
                    EndAllEventsAsync();

                    //UpdateManager stop
                    UpdateManager.StopTimers();

                    //button off
                    _control.Dispatcher.Invoke(() => _control.UpdateButtonState(false));
                    Log.Info("Session Unloading!");
                    WorldOnline = false;


                    //DiscordBot
                    if (DiscordBotConfig.EnableDiscordBot)
                    {
                        if (DiscordBot.IsConnected)
                            await DiscordBot.Client.UpdateStatusAsync(new DiscordActivity("for the server to come online...", ActivityType.Watching), UserStatus.DoNotDisturb);

                        _discordHttpServer.Stop();
                    }
                    break;
                case TorchSessionState.Unloaded:
                    break;
            }
        }

        private void InitializeManagers(ITorchSession session)
        {
            _multiplayerManager = session.Managers.GetManager<IMultiplayerManagerBase>();
            if (_multiplayerManager == null)
            {
                Log.Warn("Could not get multiplayer manager.");
            }
            else
            {
                _multiplayerManager.PlayerJoined += OnPlayerJoined;
                Log.Info("Multiplayer manager initialized.");
            }
        }

        private void ExtractAndProcessDLLFiles()
        {
            // Ustawienie ścieżki bazowej i ścieżki pluginu
            basePath = StoragePath;
            path = Path.Combine(basePath, PluginName, "DLL");

            // Tworzenie folderu pluginu, jeśli nie istnieje
            Directory.CreateDirectory(path);

            // Ścieżka do folderu z plikami tymczasowymi
            string tempFolderPath = Path.Combine(basePath, "TempExtractEventSystem");

            // Ścieżka do pliku ZIP pluginu
            string pluginZipPath = Path.Combine(basePath.Replace(@"\Instance", ""), "Plugins", "EventSystem.zip");

            try
            {
                // Usunięcie istniejącego folderu tymczasowego
                if (Directory.Exists(tempFolderPath))
                {
                    Directory.Delete(tempFolderPath, true);
                }

                // Tworzenie nowego folderu tymczasowego
                Directory.CreateDirectory(tempFolderPath);

                // Rozpakowanie pliku ZIP do folderu tymczasowego
                ZipFile.ExtractToDirectory(pluginZipPath, tempFolderPath);

                // Kopiowanie plików DLL do folderu pluginu
                foreach (var dllFile in Directory.GetFiles(tempFolderPath, "*.dll", SearchOption.AllDirectories))
                {
                    string destinationPath = Path.Combine(path, Path.GetFileName(dllFile));
                    File.Copy(dllFile, destinationPath, true);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while unpacking and processing plugin files.");
            }
            finally
            {
                // Opcjonalnie: Usuwanie folderu tymczasowego
                if (Directory.Exists(tempFolderPath))
                {
                    Directory.Delete(tempFolderPath, true);
                }
            }
        }

        private void RegisterAllEvents()
        {
            var eventTypes = Assembly.GetExecutingAssembly().GetTypes()
                            .Where(t => t.IsSubclassOf(typeof(EventsBase)) && !t.IsAbstract).ToList();

            foreach (var assembly in myAssemblies)
            {
                eventTypes.AddRange(assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(EventsBase)) && !t.IsAbstract));
            }

            foreach (var eventType in eventTypes)
            {
                try
                {
                    object eventInstance = null;

                    // Sprawdź dostępne konstruktory i wybierz odpowiedni sposób tworzenia instancji
                    var ctorWithData = eventType.GetConstructor(new[] { _config?.Data?.GetType() });
                    if (ctorWithData != null && _config?.Data != null)
                    {
                        eventInstance = Activator.CreateInstance(eventType, new object[] { _config.Data });
                    }
                    else
                    {
                        var ctorWithoutData = eventType.GetConstructor(Type.EmptyTypes);
                        if (ctorWithoutData != null)
                        {
                            eventInstance = Activator.CreateInstance(eventType);
                        }
                    }

                    if (eventInstance is EventsBase eventsBaseInstance)
                    {
                        _eventManager.RegisterEvent(eventsBaseInstance);
                    }
                    else
                    {
                        Log.Error($"Failed to create an instance of event '{eventType.Name}'.");
                    }
                }
                catch (MissingMethodException ex)
                {
                    Log.Error($"Error registering event '{eventType.Name}': No suitable constructor found. {ex.Message}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Unexpected error registering event '{eventType.Name}': {ex.GetType()}: {ex.Message}");
                }
            }
        }

        private void ScheduleAllEvents()
        {
            foreach (var eventItem in _eventManager.Events)
            {
                _eventManager.ScheduleEvent(eventItem);
            }
        }

        private async Task EndAllEventsAsync()
        {
            var activeEvents = _eventManager.Events.Where(e => e.IsActiveNow());

            var endEventTasks = activeEvents.Select(e => e.SystemEndEvent());

            await Task.WhenAll(endEventTasks);

            Log.Info("All events have been ended asynchronously.");
        }

        private async void OnPlayerJoined(IPlayer player)
        {
            if (_config.Data.UseDatabase && _databaseManager != null)
            {
                // Logika zapisywania danych gracza w bazie danych
                await _databaseManager.CreatePlayerAccountAsync(player.Name, (long)player.SteamId);
                LoggerHelper.DebugLog(Log, _config.Data, $"Player data saved in database for {player.Name}");
            }
            else
            {
                await _playerAccountXmlManager.CreatePlayerAccountAsync(player.Name, (long)player.SteamId);
                LoggerHelper.DebugLog(Log, _config.Data, $"Player account file created for {player.Name}");
            }
        }

        private void ConnectNexus()
        {
            if (!NexusInited)
            {
                PluginManager? _pluginManager = Torch.Managers.GetManager<PluginManager>();
                if (_pluginManager is null)
                    return;

                if (_pluginManager.Plugins.TryGetValue(NexusGUID, out ITorchPlugin? torchPlugin))
                {
                    if (torchPlugin is null)
                        return;

                    Type? Plugin = torchPlugin.GetType();
                    Type? NexusPatcher = Plugin != null ? Plugin.Assembly.GetType("Nexus.API.PluginAPISync") : null;
                    if (NexusPatcher != null)
                    {
                        NexusPatcher.GetMethod("ApplyPatching", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[]
                        {
                            typeof(NexusAPI), "EventSystem Plugin"
                        });
                        nexusAPI = new NexusAPI(9455);
                        MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(9455, new Action<ushort, byte[], ulong, bool>(NexusManager.HandleNexusMessage));
                        NexusInstalled = true;
                    }
                }
                NexusInited = true;
            }

            // Nowy dodany blok sprawdzający
            if (NexusInstalled && nexusAPI != null)
            {
                NexusAPI.Server thisServer = NexusAPI.GetThisServer();
                NexusManager.SetServerData(thisServer);

                if (Config!.isLobby)
                {
                    // Announce to all other servers that started before the Lobby, that this is the lobby server
                    List<NexusAPI.Server> servers = NexusAPI.GetAllServers();
                    foreach (NexusAPI.Server server in servers)
                    {
                        if (server.ServerID != thisServer.ServerID)
                        {
                            NexusMessage message = new(thisServer.ServerID, server.ServerID, false, thisServer, false, true);
                            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(message);
                            nexusAPI?.SendMessageToServer(server.ServerID, data);
                        }
                    }
                }
            }
            else
            {
                Log.Warn("Nexus API is not installed or not initialized. Skipping Nexus connection.");
            }
        }

        public void CompileAndLoadSourceCode(ITorchSession session)
        {
            var sourceCodeFolder = Path.Combine(StoragePath, "EventSystem", "EventSourceCode");
            var compilerSuccess = Compiler.Compile(sourceCodeFolder, session); // Teraz przyjmuje sesję jako argument

            if (compilerSuccess)
            {
                Log.Info("Additional source code has been successfully compiled and loaded.");
            }
            else
            {
                Log.Warn("Compilation of additional source code failed.");
            }
        }

        public long GetIdentityId(ulong steamId)
        {
            return MySession.Static.Players.TryGetIdentityId(steamId);
        }

        public bool IsPlayerOnline(long identityId)
        {
            return MySession.Static.Players.GetOnlinePlayers().Any(p => p.Identity.IdentityId == identityId);
        }


        public Task Save()
        {
            try
            {
                _config.Save();
                _packRewardsConfig.Save();
                _itemRewardsConfig.Save();
                _discordBotConfig.Save();
                Log.Info("Configuration Saved.");
            }
            catch (IOException e)
            {
                Log.Warn(" Configuration failed to save: " + e.ToString());
            }

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            DiscordBot.Dispose();
            base.Dispose();
        }
    }
}
