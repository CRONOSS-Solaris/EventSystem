using EventSystem.Utils;
using NLog;
using System;
using System.IO;
using System.Threading.Tasks;
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
                Directory.CreateDirectory(_playerAccountsFolder);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating player accounts directory");
            }
        }

        public async Task CreatePlayerAccountAsync(long steamId)
        {
            string filePath = Path.Combine(_playerAccountsFolder, $"{steamId}.xml");
            if (File.Exists(filePath)) return;

            PlayerAccount playerAccount = new PlayerAccount(steamId, 0);
            try
            {
                await SavePlayerAccountAsync(playerAccount).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error creating player account for SteamID: {steamId}");
            }
        }

        public async Task<bool> UpdatePlayerPointsAsync(long steamId, long pointsToAdd)
        {
            string filePath = Path.Combine(_playerAccountsFolder, $"{steamId}.xml");
            if (!File.Exists(filePath)) return false;

            try
            {
                PlayerAccount playerAccount = await GetPlayerAccountAsync(steamId).ConfigureAwait(false);
                if (playerAccount == null) return false;

                playerAccount.Points += pointsToAdd;
                await SavePlayerAccountAsync(playerAccount).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error updating points for player account with SteamID: {steamId}");
                return false;
            }
        }

        public async Task<PlayerAccount> GetPlayerAccountAsync(long steamId)
        {
            string filePath = Path.Combine(_playerAccountsFolder, $"{steamId}.xml");
            if (!File.Exists(filePath)) return null;

            try
            {
                return await Task.Run(() =>
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(PlayerAccount));
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        return (PlayerAccount)serializer.Deserialize(stream);
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error reading player account for SteamID: {steamId}");
                return null;
            }
        }

        private async Task SavePlayerAccountAsync(PlayerAccount playerAccount)
        {
            string filePath = Path.Combine(_playerAccountsFolder, $"{playerAccount.SteamID}.xml");
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(PlayerAccount));
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                {
                    var writer = new StreamWriter(stream);
                    serializer.Serialize(writer, playerAccount);
                    await writer.FlushAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error saving player account for SteamID: {playerAccount.SteamID}");
            }
        }
    }
}
