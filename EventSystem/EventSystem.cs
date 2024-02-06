using EventSystem.DataBase;
using EventSystem.Events;
using EventSystem.Managers;
using EventSystem.Nexus;
using EventSystem.Serialization;
using EventSystem.Utils;
using Nexus.API;
using NLog;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public class EventSystemMain : TorchPluginBase, IWpfPlugin
    {

        public static readonly Logger Log = LogManager.GetLogger("EventSystemMain");
        public static EventSystemMain Instance;

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

        //UpdateManager
        public Utils.UpdateManager UpdateManager { get; private set; }




        //Metody
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;
            //config
            var fileManager = new FileManager(Path.Combine(StoragePath, "EventSystem"));
            _config = fileManager.SetupConfig("EventSystemConfig.cfg", new EventSystemConfig());

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

            // Events
            _eventManager = new EventManager(_config?.Data, _activeEventsLCDManager, _allEventsLcdManager);
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

        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {

            switch (state)
            {
                case TorchSessionState.Loading:
                    break;

                case TorchSessionState.Loaded:
                    //Nexus
                    ConnectNexus();

                    //MultiplayerManager
                    _multiplayerManager = session.Managers.GetManager<IMultiplayerManagerBase>();
                    if (_multiplayerManager == null)
                    {
                        Log.Warn("Could not get multiplayer manager.");
                    }
                    else
                    {
                        LoggerHelper.DebugLog(Log, _config.Data, "Multiplayer manager initialized.");
                        _multiplayerManager.PlayerJoined += OnPlayerJoined;
                    }

                    //lcd
                    if (_config.Data.EnableActiveEventsLCDManager)
                    {
                        _activeEventsLCDManager = new ActiveEventsLCDManager(_eventManager, _config.Data);
                    }

                    if (_config.Data.EnableAllEventsLCDManager)
                    {
                        _allEventsLcdManager = new AllEventsLCDManager(_eventManager, _config.Data);
                    }

                    // Events
                    _eventManager = new EventManager(_config?.Data, _activeEventsLCDManager, _allEventsLcdManager);
                    // Automatyczna rejestracja eventów
                    RegisterAllEvents();

                    // Planowanie eventów po załadowaniu sesji
                    ScheduleAllEvents();

                    //UpdateManager start
                    UpdateManager = new Utils.UpdateManager();
                    UpdateManager.StartTimers();

                    //GridSpawner
                    _gridSpawner = new GridSpawner();
                    Log.Info("Session Loaded!");
                    break;

                case TorchSessionState.Unloading:
                    EndAllEvents();

                    //UpdateManager stop
                    UpdateManager.StopTimers();
                    Log.Info("Session Unloading!");
                    break;
                case TorchSessionState.Unloaded:
                    break;
            }
        }

        private void RegisterAllEvents()
        {
            // Używamy refleksji do znalezienia wszystkich klas dziedziczących po EventsBase
            var eventTypes = Assembly.GetExecutingAssembly().GetTypes()
                            .Where(t => t.IsSubclassOf(typeof(EventsBase)) && !t.IsAbstract);

            foreach (var eventType in eventTypes)
            {
                try
                {
                    // Tworzymy instancję każdego eventu
                    var eventInstance = (EventsBase)Activator.CreateInstance(eventType, _config?.Data);
                    if (eventInstance != null)
                    {
                        // Rejestrujemy event
                        _eventManager.RegisterEvent(eventInstance);
                    }
                }
                catch (Exception ex)
                {
                    // Logowanie błędu podczas tworzenia lub rejestrowania eventu
                    Log.Error($"Error registering event '{eventType.Name}': {ex.Message}");
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

        private void EndAllEvents()
        {
            foreach (var eventItem in _eventManager.Events)
            {
                if (eventItem.IsActiveNow())
                {
                    eventItem.SystemEndEvent().Wait();
                }
            }
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
                await _playerAccountXmlManager.CreatePlayerAccountAsync((long)player.SteamId);
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
                        nexusAPI = new NexusAPI(9452);
                        MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(9452, new Action<ushort, byte[], ulong, bool>(NexusManager.HandleNexusMessage));
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

        //private ConcurrentDictionary<Action, int> updateSubscribers = new ConcurrentDictionary<Action, int>();
        //private ConcurrentDictionary<Action, int> updateSubscribersPerSecond = new ConcurrentDictionary<Action, int>();

        //// Zdefiniowane czasowe aktualizacje
        //private int _currentFrameCount = 0;
        //private readonly int _maxUpdateTime = 60 * 60; // Co 1 minutę przy założeniu 60 FPS

        //private int _currentFrameCountSeconds = 0;
        //private static readonly int _maxUpdateTimeSeconds = 60; // Co 1 sekundę

        //public override void Update()
        //{
        //    _currentFrameCount++;
        //    _currentFrameCountSeconds++;

        //    if (_currentFrameCount >= _maxUpdateTime)
        //    {
        //        var orderedActions = updateSubscribers.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key);
        //        foreach (var action in orderedActions)
        //        {
        //            action();
        //        }
        //        _currentFrameCount = 0;
        //    }

        //    if (_currentFrameCountSeconds >= _maxUpdateTimeSeconds)
        //    {
        //        var orderedActionsPerSecond = updateSubscribersPerSecond.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key);
        //        foreach (var action in orderedActionsPerSecond)
        //        {
        //            action();
        //        }
        //        _currentFrameCountSeconds = 0;
        //    }
        //}

        //// Dodaj subskrybenta aktualizacji
        //public void AddUpdateSubscriber(Action updateAction, int priority = 0)
        //{
        //    updateSubscribers.TryAdd(updateAction, priority);
        //}

        //// Usuń subskrybenta aktualizacji
        //public void RemoveUpdateSubscriber(Action updateAction)
        //{
        //    updateSubscribers.TryRemove(updateAction, out _);
        //}

        //// Dodaj subskrybenta aktualizacji co sekundę
        //public void AddUpdateSubscriberPerSecond(Action updateAction, int priority = 0)
        //{
        //    updateSubscribersPerSecond.TryAdd(updateAction, priority);
        //}

        //// Usuń subskrybenta aktualizacji co sekundę
        //public void RemoveUpdateSubscriberPerSecond(Action updateAction)
        //{
        //    updateSubscribersPerSecond.TryRemove(updateAction, out _);
        //}

        public void Save()
        {
            try
            {
                _config.Save();
                Log.Info("Configuration Saved.");
            }
            catch (IOException e)
            {
                Log.Warn(e, "Configuration failed to save");
            }
        }
    }
}
