﻿using EventSystem.DataBase;
using EventSystem.Nexus;
using EventSystem.Utils;
using Nexus.API;
using NLog;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Xml.Serialization;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Managers;
using Torch.Session;

namespace EventSystem
{
    public class EventSystemMain : TorchPluginBase, IWpfPlugin
    {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static EventSystemMain Instance;
        private IMultiplayerManagerBase _multiplayerManager;
        public static IChatManagerServer ChatManager => TorchBase.Instance.CurrentSession.Managers.GetManager<IChatManagerServer>();

        //GUI
        private EventSystemControl _control;
        public UserControl GetControl() => _control ?? (_control = new EventSystemControl(this));

        //Config
        private Persistent<EventSystemConfig> _config;
        public EventSystemConfig Config => _config?.Data;

        //Nexus
        public static NexusAPI? nexusAPI { get; private set; }
        private static readonly Guid NexusGUID = new("28a12184-0422-43ba-a6e6-2e228611cca5");
        public static bool NexusInstalled { get; private set; } = false;

        public static bool NexusInited;

        //PostgresSQL
        private PostgresDatabaseManager _databaseManager;
        public PostgresDatabaseManager DatabaseManager => _databaseManager;

        //Metody
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;
            //config
            var configManager = new ConfigManager(Path.Combine(StoragePath, "EventSystem"));
            _config = configManager.SetupConfig("EventSystemConfig.cfg", new EventSystemConfig());

            //PostgresSQL
            if (_config.Data.UseDatabase)
            {
                // Ciąg połączenia z ustawień konfiguracji
                string connectionString = $"Host={_config.Data.DatabaseHost};Port={_config.Data.DatabasePort};Username={_config.Data.DatabaseUsername};Password={_config.Data.DatabasePassword};Database={_config.Data.DatabaseName};";
                _databaseManager = new PostgresDatabaseManager(connectionString);
                _databaseManager.InitializeDatabase();
            }

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

                    Log.Info("Session Loaded!");
                    break;

                case TorchSessionState.Unloading:
                    Log.Info("Session Unloading!");
                    break;
                case TorchSessionState.Unloaded:
                    break;
            }
        }

        private void OnPlayerJoined(IPlayer player)
        {
            // Sprawdź, czy korzystanie z bazy danych jest włączone
            if (_config.Data.UseDatabase && _databaseManager != null)
            {
                // Logika zapisywania danych gracza w bazie danych
                _databaseManager.CreatePlayerAccount(player.Name, (long)player.SteamId);
                LoggerHelper.DebugLog(Log, _config.Data, $"Player data saved in database for {player.Name}");
            }
            else
            {
                // Logika tworzenia pliku XML, jeśli baza danych nie jest używana
                string playerFolder = Path.Combine(StoragePath, "EventSystem", "PlayerAccounts");
                string fileName = $"{player.Name}-{player.SteamId}.xml";
                string filePath = Path.Combine(playerFolder, fileName);

                if (!File.Exists(filePath))
                {
                    PlayerAccount playerAccount = new PlayerAccount((long)player.SteamId, 0);

                    XmlSerializer serializer = new XmlSerializer(typeof(PlayerAccount));
                    using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        serializer.Serialize(fileStream, playerAccount);
                    }

                    LoggerHelper.DebugLog(Log, _config.Data, $"Player account file created for {player.Name}");
                }
                else
                {
                    LoggerHelper.DebugLog(Log, _config.Data, $"Player account file already exists for {player.Name}");
                }
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
