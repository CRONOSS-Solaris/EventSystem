using ProtoBuf;
using Torch;
using static EventSystem.Event.WarZone;
using static EventSystem.Event.WarZoneGrid;
//using static EventSystem.Event.ArenaTeamFight;
//using static EventSystem.Event.SpecialEvent;
// static EventSystem.Event.SpecialTwoEvent;

namespace EventSystem
{
    [ProtoContract]
    public class EventSystemConfig : ViewModel
    {
        // DebugMode: Włącza lub wyłącza tryb debugowania.
        private bool _debugMode;
        [ProtoMember(1)]
        public bool DebugMode { get => _debugMode; set => SetValue(ref _debugMode, value); }

        // isLobby: Określa, czy serwer jest lobby w kontekście Nexus.
        private bool _isLobby;
        [ProtoMember(2)]
        public bool isLobby { get => _isLobby; set => SetValue(ref _isLobby, value); }

        //
        private long _defaultOwnerGrid = 144115188075855881;
        [ProtoMember(3)]
        public long DefaultOwnerGrid { get => _defaultOwnerGrid; set => SetValue(ref _defaultOwnerGrid, value); }

        // EnableLCDManager: Włącza lub wyłącza zarządzanie LCD.
        private bool _enableActiveEventsLCDManager = true;
        [ProtoMember(4)]
        public bool EnableActiveEventsLCDManager { get => _enableActiveEventsLCDManager; set => SetValue(ref _enableActiveEventsLCDManager, value); }

        // lcdTagName: Nazwa tagu dla aktywnych eventów na ekranach LCD.
        private string _activeEventsLCDManagerTagName = "ACTIVE EVENTS";
        [ProtoMember(5)]
        public string ActiveEventsLCDManagerTagName { get => _activeEventsLCDManagerTagName; set => SetValue(ref _activeEventsLCDManagerTagName, value); }

        // EnableAllEventsLCDManager: Włącza lub wyłącza zarządzanie wszystkimi eventami na LCD.
        private bool _enableAllEventsLCDManager = true;
        [ProtoMember(6)]
        public bool EnableAllEventsLCDManager { get => _enableAllEventsLCDManager; set => SetValue(ref _enableAllEventsLCDManager, value); }

        // allEventsLcdTagName: Nazwa tagu dla wszystkich eventów na ekranach LCD.
        private string _allEventsLcdTagName = "ALL EVENTS";
        [ProtoMember(7)]
        public string AllEventsLcdTagName { get => _allEventsLcdTagName; set => SetValue(ref _allEventsLcdTagName, value); }

        // eventPrefix: Prefiks używany dla komunikatów związanych z eventami.
        private string _eventPrefix = "EVENT SYSTEM";
        [ProtoMember(8)]
        public string EventPrefix { get => _eventPrefix; set => SetValue(ref _eventPrefix, value); }

        // UseDatabase: Określa, czy używać bazy danych.
        private bool _useDatabase;
        [ProtoMember(9)]
        public bool UseDatabase {  get => _useDatabase; set => SetValue(ref _useDatabase, value); }

        // DatabaseHost: Host bazy danych.
        private string _databaseHost = "localhost";
        [ProtoMember(10)]
        public string DatabaseHost { get => _databaseHost; set => SetValue(ref _databaseHost, value); }

        // DatabasePort: Port bazy danych.
        private int _databasePort = 5432;
        [ProtoMember(11)]
        public int DatabasePort { get => _databasePort; set => SetValue(ref _databasePort, value); }

        // DatabaseName: Nazwa bazy danych.
        private string _databaseName = "mydatabase";
        [ProtoMember(12)]
        public string DatabaseName { get => _databaseName; set => SetValue(ref _databaseName, value); }

        // DatabaseUsername: Nazwa użytkownika bazy danych.
        private string _databaseUsername = "myuser";
        [ProtoMember(13)]
        public string DatabaseUsername { get => _databaseUsername; set => SetValue(ref _databaseUsername, value); }

        // DatabasePassword: Hasło do bazy danych.
        private string _databasePassword = "mypassword";
        [ProtoMember(14)]
        public string DatabasePassword { get => _databasePassword; set => SetValue(ref _databasePassword, value); }

        // Konfiguracje Eventów
        //public SpecialEventConfig SpecialEventSettings { get; set; }
        public WarZoneConfig WarZoneSettings { get; set; }
        public WarZoneGridConfig WarZoneGridSettings { get; set; }

        //public SpecialTwoEventConfig SpecialTwoEventSettings { get; set; }

        //private ArenaTeamFightConfig _arenaTeamFightSettings;
        //public ArenaTeamFightConfig ArenaTeamFightSettings
        //{
        //    get => _arenaTeamFightSettings;
        //    set => SetValue(ref _arenaTeamFightSettings, value);
        //}
    }
}
