using NLog;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EventSystem.Discord.Web
{
    public class DiscordHttpServer
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystemMain/DiscordHttpServer");
        private HttpListener _listener;
        private bool _isRunning;

        public void Start(string url)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(url);
            _listener.Start();
            _isRunning = true;
            Listen();
        }

        private async void Listen()
        {
            while (_isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;

                    //Log.Info($"Received request: {request.HttpMethod} {request.Url}");

                    if (request.QueryString["code"] != null && request.QueryString["state"] != null)
                    {
                        string code = request.QueryString["code"];
                        string state = request.QueryString["state"];
                        long steamId = long.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(state)));

                        //Log.Info($"Received code: {code}, state: {state}");
                        //Log.Info($"Steam ID retrieved: {steamId}");

                        string discordId = await DiscordAuthHelper.GetDiscordUserIdFromCode(code, state);

                        if (discordId != null)
                        {
                            //Log.Info($"Discord ID retrieved: {discordId}");
                            string successHtml = HtmlPages.SuccessPage(discordId, steamId);
                            await SendResponse(response, successHtml, "text/html");
                        }
                        else
                        {
                            Log.Warn("Failed to retrieve Discord ID");
                            string failureHtml = HtmlPages.FailurePage();
                            await SendResponse(response, failureHtml, "text/html");
                        }

                    }
                    else
                    {
                        Log.Warn("Invalid request: missing code or state parameters");
                        await SendResponse(response, "Invalid request", "text/plain", 400);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"An error occurred: {ex.Message}");
                }
            }
        }

        private async Task SendResponse(HttpListenerResponse response, string content, string contentType, int statusCode = 200)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            response.ContentType = contentType;
            response.StatusCode = statusCode;
            using (var output = response.OutputStream)
            {
                await output.WriteAsync(buffer, 0, buffer.Length);
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
        }
    }
}
