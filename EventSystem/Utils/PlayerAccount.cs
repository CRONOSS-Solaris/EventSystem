namespace EventSystem.Utils
{
    public class PlayerAccount
    {
        public ulong SteamID { get; set; }
        public long Points { get; set; }

        public PlayerAccount()
        {

        }

        public PlayerAccount(ulong steamID, long points)
        {
            SteamID = steamID;
            Points = points;
        }
    }
}
