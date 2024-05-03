using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public async Task CreatePlayerAccountAsync(string nickname, long steamId)
        {
            string filePath = Path.Combine(_playerAccountsFolder, $"{steamId}.xml");
            if (File.Exists(filePath)) return;

            PlayerAccount playerAccount = new PlayerAccount(nickname ,steamId, 0, "");
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

        public async Task LinkDiscordId(long steamId, string discordId)
        {
            var playerAccount = await GetPlayerAccountAsync(steamId);
            if (playerAccount == null) return;

            playerAccount.DiscordId = discordId;
            await SavePlayerAccountAsync(playerAccount);
        }

        public async Task<bool> HasLinkedDiscordId(long steamId)
        {
            var playerAccount = await GetPlayerAccountAsync(steamId);
            return playerAccount != null && !string.IsNullOrWhiteSpace(playerAccount.DiscordId);
        }

        public async Task<long?> GetPlayerPointsByDiscordId(string discordId)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(_playerAccountsFolder);
            FileInfo[] files = directoryInfo.GetFiles("*.xml");

            foreach (var file in files)
            {
                PlayerAccount playerAccount = await DeserializePlayerAccountAsync(file.FullName);
                if (playerAccount != null && playerAccount.DiscordId == discordId)
                {
                    return playerAccount.Points;
                }
            }

            return null;  // Return null if no matching account is found
        }

        private async Task<PlayerAccount> DeserializePlayerAccountAsync(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(PlayerAccount));
                    return await Task.Run(() => (PlayerAccount)serializer.Deserialize(stream));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error reading player account from file: {filePath}");
                return null;
            }
        }

        public async Task<List<(string Nickname, int Points)>> GetTopFiveUsersWithPoints()
        {
            var allAccounts = new List<PlayerAccount>();
            var files = Directory.GetFiles(_playerAccountsFolder, "*.xml");
            foreach (var file in files)
            {
                var account = await Task.Run(() =>
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(PlayerAccount));
                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        return (PlayerAccount)serializer.Deserialize(stream);
                    }
                });
                allAccounts.Add(account);
            }

            // Ensure conversion from long to int safely
            return allAccounts.OrderByDescending(a => a.Points).Take(5).Select(a => (a.Nickname, Points: (int)a.Points)).ToList();
        }

        public async Task<long?> GetSteamIdByDiscordId(string discordId)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(_playerAccountsFolder);
            FileInfo[] files = directoryInfo.GetFiles("*.xml");

            foreach (var file in files)
            {
                PlayerAccount playerAccount = await DeserializePlayerAccountAsync(file.FullName);
                if (playerAccount != null && playerAccount.DiscordId == discordId)
                {
                    return playerAccount.SteamID;
                }
            }

            return null;  // Return null if no matching account is found
        }

        public async Task<List<ulong>> GetAllDiscordIds()
        {
            var discordIds = new List<ulong>();
            DirectoryInfo directoryInfo = new DirectoryInfo(_playerAccountsFolder);
            FileInfo[] files = directoryInfo.GetFiles("*.xml");

            foreach (var file in files)
            {
                PlayerAccount playerAccount = await DeserializePlayerAccountAsync(file.FullName);
                if (playerAccount != null && !string.IsNullOrWhiteSpace(playerAccount.DiscordId))
                {
                    // Zakładamy, że DiscordId jest przechowywane jako string, który może być konwertowany na ulong
                    if (ulong.TryParse(playerAccount.DiscordId, out ulong discordId))
                    {
                        discordIds.Add(discordId);
                    }
                    else
                    {
                        Log.Warn($"Invalid Discord ID found in XML file: {file.Name}");
                    }
                }
            }
            return discordIds;
        }


    }
}
