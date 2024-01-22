using EventSystem.Utils;
using NLog;
using System;
using System.IO;
using System.Xml.Serialization;

namespace EventSystem.Managers
{
    public class PlayerAccountXmlManager
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/PlayerAccountXmlManager");
        private readonly string _playerAccountsFolder;

        public PlayerAccountXmlManager(string baseStoragePath)
        {
            _playerAccountsFolder = Path.Combine(baseStoragePath, "EventSystem", "PlayerAccounts");
            try
            {
                if (!Directory.Exists(_playerAccountsFolder))
                {
                    Directory.CreateDirectory(_playerAccountsFolder);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating player accounts directory");
            }
        }

        public void CreatePlayerAccount(long steamId)
        {
            string filePath = Path.Combine(_playerAccountsFolder, $"{steamId}.xml");

            try
            {
                if (!File.Exists(filePath))
                {
                    PlayerAccount playerAccount = new PlayerAccount(steamId, 0);
                    SavePlayerAccount(playerAccount);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error creating/updating player account for SteamID: {steamId}");
            }
        }

        public bool UpdatePlayerPoints(long steamId, long pointsToAdd)
        {
            string filePath = Path.Combine(_playerAccountsFolder, $"{steamId}.xml");

            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                XmlSerializer serializer = new XmlSerializer(typeof(PlayerAccount));
                PlayerAccount playerAccount;
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                {
                    playerAccount = (PlayerAccount)serializer.Deserialize(fileStream);
                }

                playerAccount.Points += pointsToAdd;

                using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
                {
                    serializer.Serialize(fileStream, playerAccount);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error updating points for player account with SteamID: {steamId}");
                return false;
            }
        }

        public PlayerAccount GetPlayerAccount(long steamId)
        {
            string filePath = Path.Combine(_playerAccountsFolder, $"{steamId}.xml");
            if (!File.Exists(filePath))
            {
                return null; // Nie znaleziono konta gracza
            }

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(PlayerAccount));
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                {
                    return (PlayerAccount)serializer.Deserialize(fileStream);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error reading player account for SteamID: {steamId}");
                return null;
            }
        }

        private void SavePlayerAccount(PlayerAccount playerAccount)
        {
            string filePath = Path.Combine(_playerAccountsFolder, $"{playerAccount.SteamID}.xml");

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(PlayerAccount));
                using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
                {
                    serializer.Serialize(fileStream, playerAccount);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error saving player account for SteamID: {playerAccount.SteamID}");
            }
        }
    }
}
