using ProtoBuf;
using Torch;

namespace EventSystem
{
    [ProtoContract]
    public class DiscordBotConfig : ViewModel
    {
        private bool _enableDiscordBot;
        public bool EnableDiscordBot { get => _enableDiscordBot; set => SetValue(ref _enableDiscordBot, value); }

        private string _discordBotName = "EventSystem";
        public string DiscordBotName { get => _discordBotName; set => SetValue(ref _discordBotName, value); }
        private string _discordHttpAdress = "serwerIP:8888";
        public string DiscordHttpAdress { get => _discordHttpAdress; set => SetValue(ref _discordHttpAdress, value); }

        private string _token = "";
        public string Token { get => _token; set => SetValue(ref _token, value); }

        private string _clientId = "";
        public string ClientId { get => _clientId; set => SetValue(ref _clientId, value); }

        private string _clientSecret = "";
        public string ClientSecret { get => _clientSecret; set => SetValue(ref _clientSecret, value); }

        private string[] _prefixes = new string[] { "#" };
        public string[] Prefixes { get => _prefixes; set => SetValue(ref _prefixes, value); }

        private string _statusMessage = "Event Monitoring";
        public string StatusMessage { get => _statusMessage; set => SetValue(ref _statusMessage, value); }

        private string _BotStatus;
        public string BotStatus { get => _BotStatus; set => SetValue(ref _BotStatus, value); }

        private string _discordServerId;
        public string DiscordServerId { get => _discordServerId; set => SetValue(ref _discordServerId, value); }
    }
}
