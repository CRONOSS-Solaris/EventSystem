using Torch;

namespace EventSystem
{
    public class EventSystemConfig : ViewModel
    {
        private bool _debugMode;
        public bool DebugMode { get => _debugMode; set => SetValue(ref _debugMode, value); }

        //Nexus
        private bool _isLobby;
        public bool isLobby { get => _isLobby; set => SetValue(ref _isLobby, value); }

        //prefix
        private string _eventPrefix = "EVENT SYSTEM";
        public string EventPrefix { get => _eventPrefix; set => SetValue(ref _eventPrefix, value); }
    }
}
