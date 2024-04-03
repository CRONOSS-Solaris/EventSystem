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
        private static void HandlePackRewardsConfigMessage(NexusMessage message)
        {
            if (message.ConfigData != null)
            {
                PackRewardsConfig receivedConfig = MyAPIGateway.Utilities.SerializeFromBinary<PackRewardsConfig>(message.ConfigData);
                UpdatePackRewardsConfig(receivedConfig);
            }
        }

        public static void SendPackRewardsConfigToAllServers(PackRewardsConfig config)
        {
            var servers = NexusAPI.GetAllServers();
            if (servers.Count == 0)
            {
                Log.Warn("SendPackRewardsConfigToAllServers: No servers available to send the PackRewardsConfig.");
                return;
            }

            foreach (var server in servers)
            {
                if (server.ServerID != ThisServer?.ServerID)
                {
                    try
                    {
                        SendPackRewardsConfigToServer(server.ServerID, config);
                        LoggerHelper.DebugLog(Log, Config, $"SendPackRewardsConfigToAllServers: PackRewardsConfig sent to server with ID: {server.ServerID}");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"SendPackRewardsConfigToAllServers: Failed to send PackRewardsConfig to server with ID: {server.ServerID}");
                    }
                }
                else
                {
                    LoggerHelper.DebugLog(Log, Config, "SendPackRewardsConfigToAllServers: Skipped sending PackRewardsConfig to the source server.");
                }
            }
        }

        public static void SendPackRewardsConfigToServer(int targetServerId, PackRewardsConfig config)
        {
            byte[] configData = MyAPIGateway.Utilities.SerializeToBinary(config);
            NexusMessage message = new NexusMessage(ThisServer.ServerID, targetServerId, configData, NexusMessage.MessageType.PackRewardsConfig);
            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(message);
            EventSystemMain.nexusAPI?.SendMessageToServer(targetServerId, data);
        }

        public static void UpdatePackRewardsConfig(PackRewardsConfig newConfig)
        {
            EventSystemMain.Instance?.UpdateConfigPackRewards(newConfig, false);
        }
    }
}
