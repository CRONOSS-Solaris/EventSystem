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
        private static void HandleEventSystemConfigMessage(NexusMessage message)
        {
            if (message.ConfigData != null)
            {
                EventSystemConfig receivedConfig = MyAPIGateway.Utilities.SerializeFromBinary<EventSystemConfig>(message.ConfigData);
                UpdateEventSystemConfig(receivedConfig);
            }
        }

        public static void SendEventSystemConfigToAllServers(EventSystemConfig config)
        {
            var servers = NexusAPI.GetAllServers();
            if (servers.Count == 0)
            {
                Log.Warn("SendEventSystemConfigToAllServers: No servers available to send the EventSystemConfig.");
                return;
            }

            foreach (var server in servers)
            {
                if (server.ServerID != ThisServer?.ServerID)
                {
                    try
                    {
                        SendEventSystemConfigToServer(server.ServerID, config);
                        LoggerHelper.DebugLog(Log, Config, $"SendEventSystemConfigToAllServers: EventSystemConfig sent to server with ID: {server.ServerID}");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"SendEventSystemConfigToAllServers: Failed to send EventSystemConfig to server with ID: {server.ServerID}");
                    }
                }
                else
                {
                    LoggerHelper.DebugLog(Log, Config, "SendEventSystemConfigToAllServers: Skipped sending EventSystemConfig to the source server.");
                }
            }
        }

        public static void SendEventSystemConfigToServer(int targetServerId, EventSystemConfig config)
        {
            byte[] configData = MyAPIGateway.Utilities.SerializeToBinary(config);
            NexusMessage message = new NexusMessage(ThisServer.ServerID, targetServerId, configData, NexusMessage.MessageType.EventSystemConfig);
            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(message);
            EventSystemMain.nexusAPI?.SendMessageToServer(targetServerId, data);
        }

        private static void UpdateEventSystemConfig(EventSystemConfig newConfig)
        {
            EventSystemMain.Instance?.UpdateEventSystemConfig(newConfig, false);
        }
    }
}
