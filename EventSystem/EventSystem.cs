using EventSystem.Utils;
using NLog;
using System;
using System.IO;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Session;

namespace EventSystem
{
    public class EventSystemMain : TorchPluginBase, IWpfPlugin
    {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static EventSystemMain Instance;

        private EventSystemControl _control;
        public UserControl GetControl() => _control ?? (_control = new EventSystemControl(this));

        //Config
        private Persistent<EventSystemConfig> _config;
        public EventSystemConfig Config => _config?.Data;

        //Metody
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;
            //config
            var configManager = new ConfigManager(Path.Combine(StoragePath, "EventSystem"));
            _config = configManager.SetupConfig("EventSystemConfig.cfg", new EventSystemConfig());

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
                    Log.Info("Session Loaded!");
                    break;

                case TorchSessionState.Unloading:
                    Log.Info("Session Unloading!");
                    break;
                case TorchSessionState.Unloaded:
                    break;
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
