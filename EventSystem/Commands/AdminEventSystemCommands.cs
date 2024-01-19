using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using System;
using System.IO;
using System.Xml.Serialization;
using EventSystem.Utils;
using Torch.API.Managers;
using VRageMath;
using NLog.Fluent;
using EventSystem.DataBase;

namespace EventSystem
{
    [Category("EventAdmin")]
    public class AdminEventSystemCommands : CommandModule
    {
        public EventSystemMain Plugin => (EventSystemMain)Context.Plugin;

        [Command("modifypoints", "Add or remove points from a player's account.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ModifyPoints(string playerNameOrSteamId, long points)
        {
            ulong steamId;
            bool isNumeric = ulong.TryParse(playerNameOrSteamId, out steamId);

            if (Plugin.Config.UseDatabase)
            {
                // Logika używania bazy danych
                if (!isNumeric)
                {
                    var player = Context.Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerBase>().GetPlayerByName(playerNameOrSteamId);
                    if (player == null)
                    {
                        EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Player not found: {playerNameOrSteamId}.", Color.Red, Context.Player.SteamUserId);
                        return;
                    }
                    steamId = player.SteamUserId;
                }

                Plugin.DatabaseManager.UpdatePlayerPoints(playerNameOrSteamId, points);
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Modified points for {playerNameOrSteamId}.", Color.Green, Context.Player.SteamUserId);
            }
            else
            {
                // Obecna logika plików XML
                string fileName = isNumeric ? $"{playerNameOrSteamId}.xml" : $"{Context.Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerBase>().GetPlayerBySteamId(steamId).DisplayName}-{steamId}.xml";
                string playerFolder = Path.Combine(Plugin.StoragePath, "EventSystem", "PlayerAccounts");
                string filePath = Path.Combine(playerFolder, fileName);

                if (!File.Exists(filePath))
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Player account file does not exist for {playerNameOrSteamId}.", Color.Red, Context.Player.SteamUserId);
                    return;
                }

                XmlSerializer serializer = new XmlSerializer(typeof(PlayerAccount));
                PlayerAccount playerAccount;
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                {
                    playerAccount = (PlayerAccount)serializer.Deserialize(fileStream);
                }

                playerAccount.Points += points;

                using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
                {
                    serializer.Serialize(fileStream, playerAccount);
                }

                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Modified points for {playerNameOrSteamId}. New total: {playerAccount.Points}", Color.Green, Context.Player.SteamUserId);
            }
        }
    }
}
