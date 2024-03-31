//using EventSystem.Config;
//using EventSystem.Utils;
//using Nexus.API;
//using Sandbox.ModAPI;
//using System;
//#nullable enable 

//namespace EventSystem.Nexus
//{
//    public static partial class NexusManager
//    {
//        private static void HandleConfigurationMessage(NexusMessage message)
//        {
//            switch (message.Type)
//            {
//                case NexusMessage.MessageType.BaseConfig:
//                    if (message.ConfigData != null)
//                    {
//                        var receivedConfig = MyAPIGateway.Utilities.SerializeFromBinary<EventSystemConfig>(message.ConfigData);
//                        UpdateConfiguration(receivedConfig);
//                    }
//                    break;
//                case NexusMessage.MessageType.ItemRewardsConfig:
//                    if (message.ConfigData != null)
//                    {
//                        var receivedItemRewardsConfig = MyAPIGateway.Utilities.SerializeFromBinary<ItemRewardsConfig>(message.ConfigData);
//                        UpdateItemRewardsConfiguration(receivedItemRewardsConfig);
//                    }
//                    break;
//                case NexusMessage.MessageType.PackRewardsConfig:
//                    if (message.ConfigData != null)
//                    {
//                        var receivedPackRewardsConfig = MyAPIGateway.Utilities.SerializeFromBinary<PackRewardsConfig>(message.ConfigData);
//                        UpdatePackRewardsConfiguration(receivedPackRewardsConfig);
//                    }
//                    break;
//                    // Możesz dodać więcej przypadków, jeśli masz więcej typów konfiguracji
//            }
//        }


//        public static void SendAllConfigsToAllServers()
//        {
//            var servers = NexusAPI.GetAllServers();
//            if (servers.Count == 0)
//            {
//                Log.Warn("SendAllConfigsToAllServers: No servers available to send the configurations.");
//                return;
//            }

//            foreach (var server in servers)
//            {
//                if (server.ServerID != ThisServer?.ServerID)
//                {
//                    try
//                    {
//                        // Wysyłanie konfiguracji bazy danych
//                        SendConfigToServer(server.ServerID, Config, NexusMessage.MessageType.BaseConfig);

//                        // Przykład wysyłania ItemRewardsConfig
//                        var itemRewardsConfig = new ItemRewardsConfig(); // Pobierz aktualną konfigurację nagród przedmiotów
//                        SendConfigToServer(server.ServerID, itemRewardsConfig, NexusMessage.MessageType.ItemRewardsConfig);

//                        // Przykład wysyłania PackRewardsConfig
//                        var packRewardsConfig = new PackRewardsConfig(); // Pobierz aktualną konfigurację zestawów nagród
//                        SendConfigToServer(server.ServerID, packRewardsConfig, NexusMessage.MessageType.PackRewardsConfig);

//                        LoggerHelper.DebugLog(Log, Config, $"SendAllConfigsToAllServers: Configurations sent to server with ID: {server.ServerID}");
//                    }
//                    catch (Exception ex)
//                    {
//                        Log.Error(ex, $"SendAllConfigsToAllServers: Failed to send configurations to server with ID: {server.ServerID}");
//                    }
//                }
//                else
//                {
//                    LoggerHelper.DebugLog(Log, Config, "SendAllConfigsToAllServers: Skipped sending configurations to the source server.");
//                }
//            }
//        }


//        public static void SendConfigToServer<T>(int targetServerId, T config, NexusMessage.MessageType configType)
//        {
//            byte[] configData = MyAPIGateway.Utilities.SerializeToBinary(config);
//            NexusMessage message = new NexusMessage(ThisServer?.ServerID ?? 0, targetServerId, configData, configType);
//            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(message);
//            EventSystemMain.nexusAPI?.SendMessageToServer(targetServerId, data);
//        }

//        private static void UpdateConfiguration(EventSystemConfig newConfig)
//        {
//            EventSystemMain.Instance?.UpdateConfig(newConfig, false);
//        }

//        private static void UpdateItemRewardsConfiguration(ItemRewardsConfig newConfig)
//        {
//            EventSystemMain.Instance?.UpdateConfigItemRewards(newConfig, false);
//        }

//        private static void UpdatePackRewardsConfiguration(PackRewardsConfig newConfig)
//        {
//            EventSystemMain.Instance?.UpdateConfigPackRewards(newConfig, false);
//        }
//    }
//}
