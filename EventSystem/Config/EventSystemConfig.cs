using EventSystem.Utils;
using System;
using System.Collections.Generic;
using Torch;
using static EventSystem.Event.SpecialEvent;

namespace EventSystem
{
    public class EventSystemConfig : ViewModel
    {
        private bool _debugMode;
        public bool DebugMode { get => _debugMode; set => SetValue(ref _debugMode, value); }

        //Nexus
        private bool _isLobby;
        public bool isLobby { get => _isLobby; set => SetValue(ref _isLobby, value); }

        //lcdTagName
        private string _lcdTagName = "ACTIVE EVENTS";
        public string lcdTagName { get => _lcdTagName; set => SetValue(ref _lcdTagName, value); }

        //prefix
        private string _eventPrefix = "EVENT SYSTEM";
        public string EventPrefix { get => _eventPrefix; set => SetValue(ref _eventPrefix, value); }

        //PostgresSQL
        private bool _useDatabase;
        public bool UseDatabase
        {
            get => _useDatabase;
            set => SetValue(ref _useDatabase, value);
        }

        private string _databaseHost = "localhost";
        public string DatabaseHost
        {
            get => _databaseHost;
            set => SetValue(ref _databaseHost, value);
        }

        private int _databasePort = 5432;
        public int DatabasePort
        {
            get => _databasePort;
            set => SetValue(ref _databasePort, value);
        }

        private string _databaseName = "mydatabase";
        public string DatabaseName
        {
            get => _databaseName;
            set => SetValue(ref _databaseName, value);
        }

        private string _databaseUsername = "myuser";
        public string DatabaseUsername
        {
            get => _databaseUsername;
            set => SetValue(ref _databaseUsername, value);
        }

        private string _databasePassword = "mypassword";
        public string DatabasePassword
        {
            get => _databasePassword;
            set => SetValue(ref _databasePassword, value);
        }

        //Events
        public SpecialEventConfig SpecialEventSettings { get; set; }

    }
}
