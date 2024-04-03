using EventSystem.Config;
using EventSystem.Utils;
using Nexus.API;
using Sandbox.ModAPI;
using System;
#nullable enable 

namespace EventSystem.Nexus
{
    public static partial class NexusManager
    {
        private static void HandleItemRewardsConfigMessage(NexusMessage message)
        {
            if (message.ConfigData != null)
            {
                ItemRewardsConfig receivedConfig = MyAPIGateway.Utilities.SerializeFromBinary<ItemRewardsConfig>(message.ConfigData);
                UpdateItemRewardsConfig(receivedConfig);
            }
        }

        public static void SendItemRewardsConfigToAllServers(ItemRewardsConfig config)
        {
            var servers = NexusAPI.GetAllServers();
            if (servers.Count == 0)
            {
                Log.Warn("SendItemRewardsConfigToAllServers: No servers available to send the ItemRewardsConfig.");
                return;
            }

            foreach (var server in servers)
            {
                if (server.ServerID != ThisServer?.ServerID)
                {
                    try
                    {
                        SendItemRewardsConfigToServer(server.ServerID, config);
                        LoggerHelper.DebugLog(Log, Config, $"SendItemRewardsConfigToAllServers: ItemRewardsConfig sent to server with ID: {server.ServerID}");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"SendItemRewardsConfigToAllServers: Failed to send ItemRewardsConfig to server with ID: {server.ServerID}");
                    }
                }
                else
                {
                    LoggerHelper.DebugLog(Log, Config, "SendItemRewardsConfigToAllServers: Skipped sending ItemRewardsConfig to the source server.");
                }
            }
        }

        public static void SendItemRewardsConfigToServer(int targetServerId, ItemRewardsConfig config)
        {
            byte[] configData = MyAPIGateway.Utilities.SerializeToBinary(config);
            NexusMessage message = new NexusMessage(ThisServer.ServerID, targetServerId, configData, NexusMessage.MessageType.ItemRewardsConfig);
            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(message);
            EventSystemMain.nexusAPI?.SendMessageToServer(targetServerId, data);
        }

        private static void UpdateItemRewardsConfig(ItemRewardsConfig newConfig)
        {
            EventSystemMain.Instance?.UpdateItemRewardsConfig(newConfig, false);
        }
    }
}
