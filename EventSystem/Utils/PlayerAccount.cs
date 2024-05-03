namespace EventSystem.Utils
{
    public class PlayerAccount
    {
        public string Nickname { get; set; }
        public long SteamID { get; set; }
        public long Points { get; set; }
        public string DiscordId { get; set; }

        public PlayerAccount()
        {

        }

        public PlayerAccount(string nickname, long steamID, long points, string discordId)
        {
            Nickname = nickname;
            SteamID = steamID;
            Points = points;
            DiscordId = discordId;
        }
    }
}
