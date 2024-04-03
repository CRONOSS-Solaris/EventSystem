using EventSystem.Utils;
using Nexus.API;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EventSystem.Nexus
{
    public static partial class NexusManager
    {
        private static void HandleGPSEventMessage(NexusMessage message)
        {
            if (message.Data != null)
            {
                GPSEventData receivedGPSData = MyAPIGateway.Utilities.SerializeFromBinary<GPSEventData>(message.Data);

                // Tutaj można zaimplementować logikę, która zdecyduje, czy GPS ma być dodany do konkretnego gracza,
                // na przykład na podstawie jego playerId, lub dodać GPS do wszystkich graczy online

                foreach (var player in MySession.Static.Players.GetOnlinePlayers()?.ToList() ?? new List<MyPlayer>())
                {
                    long playerId = player.Identity.IdentityId;
                    // Dodaj GPS do gracza, upewniając się, że nie rozgłaszamy ponownie do innych serwerów
                    EventSystemMain.Instance.EventsBase.SendGpsToPlayer(playerId, receivedGPSData.Name, receivedGPSData.Coords, receivedGPSData.Description, receivedGPSData.DiscardAt, receivedGPSData.ShowOnHud, receivedGPSData.AlwaysVisible, receivedGPSData.Color, receivedGPSData.EntityId, receivedGPSData.IsObjective, receivedGPSData.ContractId, false);

                }
            }
        }

        public static void SendGPSEventToAllServers(GPSEventData gpsData)
        {
            var servers = NexusAPI.GetAllServers();
            if (servers.Count == 0)
            {
                Log.Warn("SendGPSEventToAllServers: No servers available to send the GPSEvent.");
                return;
            }

            foreach (var server in servers)
            {
                if (server.ServerID != ThisServer?.ServerID)
                {
                    try
                    {
                        SendGPSEventToServer(server.ServerID, gpsData);
                        LoggerHelper.DebugLog(Log, Config, $"SendGPSEventToAllServers: GPSEvent sent to server with ID: {server.ServerID}");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(ex, $"SendGPSEventToAllServers: Failed to send GPSEvent to server with ID: {server.ServerID}");
                    }
                }
                else
                {
                    LoggerHelper.DebugLog(Log, Config, "SendGPSEventToAllServers: Skipped sending GPSEvent to the source server.");
                }
            }
        }

        public static void SendGPSEventToServer(int targetServerId, GPSEventData gpsData)
        {
            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(gpsData);
            NexusMessage message = new NexusMessage(ThisServer.ServerID, targetServerId, data, NexusMessage.MessageType.GPSEvent);
            byte[] serializedMessage = MyAPIGateway.Utilities.SerializeToBinary(message);
            EventSystemMain.nexusAPI?.SendMessageToServer(targetServerId, serializedMessage);
        }
    }
}
