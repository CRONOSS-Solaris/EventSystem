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
using static Nexus.API.NexusAPI;

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
            string fileName;

            if (isNumeric)
            {
                fileName = $"{playerNameOrSteamId}.xml";
            }
            else
            {
                var player = Context.Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerBase>().GetPlayerByName(playerNameOrSteamId);
                if (player == null)
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Player not found: {playerNameOrSteamId}.", Color.Red, Context.Player.SteamUserId);
                    return;
                }
                steamId = player.SteamUserId;
                fileName = $"{player.DisplayName}-{steamId}.xml";
            }

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
