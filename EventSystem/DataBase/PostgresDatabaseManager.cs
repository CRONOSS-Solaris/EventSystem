using Npgsql;
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


        // Dodatkowe metody do obsługi zaawansowanych zapytań SQL
    }
}
