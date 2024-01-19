using Npgsql;
using System;
using System.Data;

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
                            points BIGINT NOT NULL
                        )";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void CreatePlayerAccount(string nickname, long steamId, long initialPoints = 0)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = connection;
                    cmd.CommandText = "INSERT INTO player_accounts (nickname, steam_id, points) VALUES (@nickname, @steamId, @points) ON CONFLICT (steam_id) DO NOTHING";
                    cmd.Parameters.AddWithValue("@nickname", nickname);
                    cmd.Parameters.AddWithValue("@steamId", steamId);
                    cmd.Parameters.AddWithValue("@points", initialPoints);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdatePlayerPoints(string playerNameOrSteamId, long pointsToAdd)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = connection;

                    if (long.TryParse(playerNameOrSteamId, out long steamId))
                    {
                        // Aktualizacja punktów na podstawie Steam ID
                        cmd.CommandText = "UPDATE player_accounts SET points = points + @pointsToAdd WHERE steam_id = @steamId";
                        cmd.Parameters.AddWithValue("@steamId", steamId);
                    }
                    else
                    {
                        // Aktualizacja punktów na podstawie nickname
                        cmd.CommandText = "UPDATE player_accounts SET points = points + @pointsToAdd WHERE nickname = @nickname";
                        cmd.Parameters.AddWithValue("@nickname", playerNameOrSteamId);
                    }

                    cmd.Parameters.AddWithValue("@pointsToAdd", pointsToAdd);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public long GetPlayerPoints(long steamId)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = new NpgsqlCommand("SELECT points FROM player_accounts WHERE steam_id = @steamId", connection))
                {
                    cmd.Parameters.AddWithValue("@steamId", steamId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetInt64(0);
                        }
                    }
                }
            }

            return 0; // Jeśli gracz nie istnieje w bazie
        }

        // Dodatkowe metody do obsługi zaawansowanych zapytań SQL
    }
}
