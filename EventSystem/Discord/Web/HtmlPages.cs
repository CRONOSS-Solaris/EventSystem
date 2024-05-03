using System;

namespace EventSystem.Discord.Web
{
    public static class HtmlPages
    {
        private static string BaseHtml(string title, string bodyContent)
        {
            return $@"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>{title}</title>
                <style>
                    body {{ font-family: 'Helvetica', 'Arial', sans-serif; background-color: #23272a; color: #ffffff; text-align: center; padding: 50px; }}
                    h1 {{ color: #7289da; }}
                    p {{ color: #99aab5; }}
                </style>
            </head>
            <body>
                {bodyContent}
            </body>
            </html>";
        }

        public static string SuccessPage(string discordId, long steamId)
        {
            var content = $@"
                <h1>Event System Login Successful</h1>
                <img src='https://i.imgur.com/2YrwNXa.png' alt='Event System Logo' style='width:100px; height:auto;'>
                <p>Discord ID: {discordId}</p>
                <p>Steam ID: {steamId}</p>";
            return BaseHtml("Login Successful", content);
        }

        public static string FailurePage()
        {
            var content = @"
                <h1>Event System Login Failed</h1>
                <img src='https://i.imgur.com/2YrwNXa.png' alt='Event System Logo' style='width:100px; height:auto;'>
                <p>Unable to retrieve your Discord ID. Please try again or contact support if the problem persists.</p>";
            return BaseHtml("Login Failed", content);
        }
    }
}
