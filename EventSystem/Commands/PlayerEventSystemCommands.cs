using Torch.Commands;
using Torch.Commands.Permissions;
using System;
using System.IO;
using System.Xml.Serialization;
using EventSystem.Utils;
using VRage.Game.ModAPI;
using NLog.Fluent;
using VRageMath;
using System.Text;
using EventSystem.DataBase; // Dodaj tę linię

namespace EventSystem
{
    [Category("Event")]
    public class PlayerEventSystemCommands : CommandModule
    {
        public EventSystemMain Plugin => (EventSystemMain)Context.Plugin;

        [Command("checkpoints", "Check your points.")]
        [Permission(MyPromoteLevel.None)]
        public void CheckPoints()
        {
            if (Context.Player == null)
            {
                Log.Error("This command can only be used by a player.");
                return;
            }

            long steamId = (long)Context.Player.SteamUserId;

            if (Plugin.Config.UseDatabase)
            {
                // Logika używania bazy danych
                long points = Plugin.DatabaseManager.GetPlayerPoints(steamId);
                string response = new StringBuilder()
                    .AppendLine()
                    .AppendLine("---------------------")
                    .AppendLine("Available pts")
                    .AppendLine($"- {points}")
                    .AppendLine("---------------------")
                    .ToString();

                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", response, Color.Green, Context.Player.SteamUserId);
            }
            else
            {
                // Logika plików XML
                string fileName = $"{steamId}.xml";
                string playerFolder = Path.Combine(Plugin.StoragePath, "EventSystem", "PlayerAccounts");
                string filePath = Path.Combine(playerFolder, fileName);

                if (!File.Exists(filePath))
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "You do not have an account file.", Color.Red, Context.Player.SteamUserId);
                    return;
                }

                XmlSerializer serializer = new XmlSerializer(typeof(PlayerAccount));
                PlayerAccount playerAccount;
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                {
                    playerAccount = (PlayerAccount)serializer.Deserialize(fileStream);
                }

                string response = new StringBuilder()
                    .AppendLine()
                    .AppendLine("---------------------")
                    .AppendLine("Available pts")
                    .AppendLine($"- {playerAccount.Points}")
                    .AppendLine("---------------------")
                    .ToString();

                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", response, Color.Green, Context.Player.SteamUserId);
            }
        }
    }
}
