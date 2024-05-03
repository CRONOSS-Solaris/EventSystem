using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSystem.DataBase
{
    public class PostgresDatabaseManager
    {
        private readonly string _connectionString;

        public PostgresDatabaseManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void InitializeDatabase()
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = connection;

                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS eventsystem_player_accounts (
                            nickname VARCHAR(255),
                            steam_id BIGINT PRIMARY KEY,
                            discord_id BIGINT,
                            points BIGINT NOT NULL
                        )";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public async Task CreatePlayerAccountAsync(string nickname, long steamId, long initialPoints = 0)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = connection;
                    cmd.CommandText = "INSERT INTO eventsystem_player_accounts (nickname, steam_id, points) VALUES (@nickname, @steamId, @points) ON CONFLICT (steam_id) DO NOTHING";
                    cmd.Parameters.AddWithValue("@nickname", nickname);
                    cmd.Parameters.AddWithValue("@steamId", steamId);
                    cmd.Parameters.AddWithValue("@points", initialPoints);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<bool> UpdatePlayerPointsAsync(string playerNameOrSteamId, long pointsToAdd)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = connection;

                    string sql;
                    if (long.TryParse(playerNameOrSteamId, out long steamId))
                    {
                        sql = "UPDATE eventsystem_player_accounts SET points = points + @pointsToAdd WHERE steam_id = @steamId";
                        cmd.Parameters.AddWithValue("@steamId", steamId);
                    }
                    else
                    {
                        sql = "UPDATE eventsystem_player_accounts SET points = points + @pointsToAdd WHERE nickname = @nickname";
                        cmd.Parameters.AddWithValue("@nickname", playerNameOrSteamId);
                    }
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@pointsToAdd", pointsToAdd);

                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
        }


        public async Task<long?> GetPlayerPointsAsync(long steamId)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand("SELECT points FROM eventsystem_player_accounts WHERE steam_id = @steamId", connection))
                {
                    cmd.Parameters.AddWithValue("@steamId", steamId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return reader.GetInt64(0);
                        }
                    }
                }
            }
            return null;
        }

        public async Task LinkDiscordId(long steamId, string discordId)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand("UPDATE eventsystem_player_accounts SET discord_id = @discordId WHERE steam_id = @steamId", connection))
                {
                    cmd.Parameters.AddWithValue("@discordId", discordId);
                    cmd.Parameters.AddWithValue("@steamId", steamId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<bool> HasLinkedDiscordId(long steamId)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand("SELECT discord_id FROM eventsystem_player_accounts WHERE steam_id = @steamId", connection))
                {
                    cmd.Parameters.AddWithValue("@steamId", steamId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var discordId = reader["discord_id"];
                            return discordId != DBNull.Value && !string.IsNullOrWhiteSpace(discordId.ToString());
                        }
                    }
                }
            }
            return false;
        }

        public async Task<long?> GetPlayerPointsByDiscordId(string discordId)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand("SELECT points FROM eventsystem_player_accounts WHERE discord_id = @discordId", connection))
                {
                    cmd.Parameters.AddWithValue("@discordId", discordId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return reader.GetInt64(0);
                        }
                    }
                }
            }
            return null; // Return null if no record is found or in case of an error
        }

        public async Task<List<(string Username, int Points)>> GetTopFiveUsersWithPoints()
        {
            var topUsers = new List<(string Username, int Points)>();
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand("SELECT username, points FROM eventsystem_player_accounts ORDER BY points DESC LIMIT 5", connection))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            topUsers.Add((reader.GetString(0), reader.GetInt32(1)));
                        }
                    }
                }
            }
            return topUsers;
        }

        public async Task<long?> GetSteamIdByDiscordId(string discordIdStr)
        {
            if (long.TryParse(discordIdStr, out long discordId))
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var cmd = new NpgsqlCommand("SELECT steam_id FROM eventsystem_player_accounts WHERE discord_id = @discordId", connection))
                    {
                        cmd.Parameters.AddWithValue("@discordId", discordId); // Użyj skonwertowanego long
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync() && !reader.IsDBNull(0))
                            {
                                return reader.GetInt64(0);
                            }
                        }
                    }
                }
            }
            return null; // Zwróć null jeśli konwersja się nie powiedzie lub rekord nie zostanie znaleziony
        }

        public async Task<List<ulong>> GetAllDiscordIds()
        {
            List<ulong> discordIds = new List<ulong>();
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new NpgsqlCommand("SELECT discord_id FROM eventsystem_player_accounts WHERE discord_id IS NOT NULL", connection))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Sprawdzenie czy wartość discord_id nie jest null i dodanie do listy
                            if (!reader.IsDBNull(0)) // Zakładając, że discord_id jest w pierwszej kolumnie
                            {
                                discordIds.Add((ulong)reader.GetInt64(0));
                            }
                        }
                    }
                }
            }
            return discordIds;
        }


        // Dodatkowe metody do obsługi zaawansowanych zapytań SQL
    }
}
