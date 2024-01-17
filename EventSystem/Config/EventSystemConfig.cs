using Torch;

namespace EventSystem
{
    public class EventSystemConfig : ViewModel
    {
        private bool _debugMode = false;

        public bool DebugMode
        {
            get => _debugMode;
            set => SetValue(ref _debugMode, value);
        }
    }
}
