using Nexus.API;
using ProtoBuf;

namespace EventSystem.Nexus
{
    [ProtoContract]
    public class NexusMessage
    {
        public enum MessageType
        {
            BaseConfig,
            ItemRewardsConfig,
            PackRewardsConfig,
            // ...
        }

        [ProtoMember(1)]
        public byte[]? ConfigData { get; set; }

        [ProtoMember(2)]
        public MessageType Type { get; set; }

        [ProtoMember(3)]
        public byte[]? Data { get; set; }

        public readonly int fromServerID;
        public readonly int toServerID;
        public readonly bool isTestAnnouncement;
        public readonly bool requestLobbyServer;
        public readonly NexusAPI.Server? lobbyServerData;
        public readonly bool isLobbyReply;

        public NexusMessage()
        {
        }

        public NexusMessage(int _fromServerId, int _toServerId, byte[] data, MessageType messageType)
        {
            fromServerID = _fromServerId;
            toServerID = _toServerId;
            isTestAnnouncement = false;
            requestLobbyServer = false;
            isLobbyReply = false;
            lobbyServerData = null;


            if (messageType == MessageType.BaseConfig)
            {
                ConfigData = data;
                Data = new byte[0];

            }
            else if (messageType == MessageType.ItemRewardsConfig)
            {
                ConfigData = data;
                Data = new byte[0];
            }
            else if (messageType == MessageType.PackRewardsConfig)
            {
                ConfigData = data;
                Data = new byte[0];
            }

            Type = messageType;
        }

        public NexusMessage(int _fromServerId, int _toServerId, bool _isTestAnnouncement, NexusAPI.Server? _lobbyServerData, bool _requestLobbyServer, bool _isLobbyReply)
        {
            fromServerID = _fromServerId;
            toServerID = _toServerId;
            isTestAnnouncement = _isTestAnnouncement;
            requestLobbyServer = _requestLobbyServer;
            isLobbyReply = _isLobbyReply;
            lobbyServerData = _lobbyServerData;

            // Ponieważ ten konstruktor nie dotyczy wiadomości konfiguracyjnych, ustawiamy odpowiednie wartości domyślne.
            ConfigData = new byte[0];  // lub jakiekolwiek inne domyślne wartości, które są odpowiednie w kontekście
            Type = MessageType.BaseConfig; // lub inny domyślny typ wiadomości,
        }
    }
}
