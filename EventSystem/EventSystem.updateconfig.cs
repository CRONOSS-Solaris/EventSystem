﻿//using EventSystem.Config;
//using EventSystem.Nexus;
//using Torch;
//using Torch.API.Plugins;

//namespace EventSystem
//{
//    public partial class EventSystemMain : TorchPluginBase, IWpfPlugin
//    {
//        public void UpdateConfig(EventSystemConfig newConfig, bool propagateToServers = true)
//        {
//            if (_config?.Data == null)
//            {
//                Log.Warn("Config is not initialized.");
//                return;
//            }

//            _config.Data.DebugMode = newConfig.DebugMode;
//            _config.Data.isLobby = newConfig.isLobby;
//            _config.Data.DefaultOwnerGrid = newConfig.DefaultOwnerGrid;
//            _config.Data.EnableActiveEventsLCDManager = newConfig.EnableActiveEventsLCDManager;
//            _config.Data.ActiveEventsLCDManagerTagName = newConfig.ActiveEventsLCDManagerTagName;
//            _config.Data.EnableAllEventsLCDManager = newConfig.EnableAllEventsLCDManager;
//            _config.Data.AllEventsLcdTagName = newConfig.AllEventsLcdTagName;
//            _config.Data.EventPrefix = newConfig.EventPrefix;
//            _config.Data.UseDatabase = newConfig.UseDatabase;
//            _config.Data.DatabaseHost = newConfig.DatabaseHost;
//            _config.Data.DatabasePort = newConfig.DatabasePort;
//            _config.Data.DatabaseName = newConfig.DatabaseName;
//            _config.Data.DatabaseUsername = newConfig.DatabaseUsername;
//            _config.Data.DatabasePassword = newConfig.DatabasePassword;


//            if (propagateToServers)
//            {
//                NexusManager.SendAllConfigsToAllServers();
//            }
//        }

//        public void UpdateConfigItemRewards(ItemRewardsConfig newConfig, bool propagateToServers = true)
//        {
//            if (_config?.Data == null)
//            {
//                Log.Warn("Config is not initialized.");
//                return;
//            }

//            _itemRewardsConfig.Data.IndividualItems = newConfig.IndividualItems;

//            if (propagateToServers)
//            {
//                NexusManager.SendAllConfigsToAllServers();
//            }
//        }

//        public void UpdateConfigPackRewards(PackRewardsConfig newConfig, bool propagateToServers = true)
//        {
//            if (_config?.Data == null)
//            {
//                Log.Warn("Config is not initialized.");
//                return;
//            }

//            _packRewardsConfig.Data.RewardSets = newConfig.RewardSets;

//            if (propagateToServers)
//            {
//                NexusManager.SendAllConfigsToAllServers();
//            }
//        }
//    }
//}
