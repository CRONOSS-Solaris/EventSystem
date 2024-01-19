namespace EventSystem.Utils
{
    public class PlayerAccount
    {
        public long SteamID { get; set; }
        public long Points { get; set; }

        public PlayerAccount()
        {

        }

        public PlayerAccount(long steamID, long points)
        {
            SteamID = steamID;
            Points = points;
        }
    }
}
