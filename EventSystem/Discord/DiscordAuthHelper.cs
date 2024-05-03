using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NLog.Fluent;

namespace EventSystem.Discord
{
    public class DiscordAuthHelper
    {
        private static readonly string clientId = EventSystemMain.Instance.DiscordBotConfig.ClientId;
        private static readonly string clientSecret = EventSystemMain.Instance.DiscordBotConfig.ClientSecret;
        private static readonly string redirectUri = $"http://{EventSystemMain.Instance.DiscordBotConfig.DiscordHttpAdress}/auth/";

        public static string GetDiscordLoginUrl(long steamId)
        {
            string state = Convert.ToBase64String(Encoding.UTF8.GetBytes(steamId.ToString()));
            return $"https://discord.com/api/oauth2/authorize?client_id={clientId}&redirect_uri={Uri.EscapeUriString(redirectUri)}&response_type=code&scope=identify&state={state}";
        }

        public static async Task<string> GetDiscordUserIdFromCode(string code, string state)
        {
            long steamId = 0;
            try
            {
                steamId = long.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(state)));
            }
            catch (FormatException ex)
            {
                Log.Error($"Failed to decode and parse Steam ID from state: {ex.Message}");
                return null;
            }

            try
            {
                using (var client = new HttpClient())
                {
                    var content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        {"client_id", clientId},
                        {"client_secret", clientSecret},
                        {"grant_type", "authorization_code"},
                        {"code", code},
                        {"redirect_uri", Uri.EscapeUriString(redirectUri)}  // Ensure this is used correctly
                    });
        
                    Log.Info($"Attempting to post to Discord API with redirect URI: {redirectUri}");

                    HttpResponseMessage response = await client.PostAsync("https://discord.com/api/oauth2/token", content);
                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Error($"Error response from Discord during token acquisition: {await response.Content.ReadAsStringAsync()}");
                        return null;
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    var tokenData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);
                    if (!tokenData.TryGetValue("access_token", out var accessTokenObject))
                    {
                        Log.Error("Access token not present in response.");
                        return null;
                    }
                    string accessToken = accessTokenObject.ToString();

                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    HttpResponseMessage userResponse = await client.GetAsync("https://discord.com/api/users/@me");
                    string userResponseBody = await userResponse.Content.ReadAsStringAsync();
                    if (!userResponse.IsSuccessStatusCode)
                    {
                        Log.Error($"Failed to retrieve user data: HTTP {userResponse.StatusCode} - {userResponseBody}");
                        return null;
                    }

                    var user = JsonConvert.DeserializeObject<Dictionary<string, object>>(userResponseBody);
                    if (!user.TryGetValue("id", out var discordIdObject))
                    {
                        Log.Error("Discord ID not found in the user response.");
                        return null;
                    }

                    if (EventSystemMain.Instance.Config.UseDatabase)
                    {
                        await EventSystemMain.Instance.DatabaseManager.LinkDiscordId(steamId, (string)discordIdObject);
                    }
                    else
                    {
                        await EventSystemMain.Instance.PlayerAccountXmlManager.LinkDiscordId(steamId, (string)discordIdObject);
                    }

                    return discordIdObject.ToString();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred in processing Discord login: {ex.Message}");
                return null;
            }
        }


    }
}
