using EventSystem.Utils;
using System.IO;
using System.Xml.Serialization;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRageMath;

namespace EventSystem
{
    [Category("EventAdmin")]
    public class AdminEventSystemCommands : CommandModule
    {
        public EventSystemMain Plugin => (EventSystemMain)Context.Plugin;

        [Command("modifypoints", "Add or remove points from a player's account.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ModifyPoints(ulong steamId, long points)
        {
            if (Plugin.Config.UseDatabase)
            {
                // Logika używania bazy danych
                Plugin.DatabaseManager.UpdatePlayerPoints(steamId.ToString(), points);
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Modified points for {steamId}.", Color.Green, Context.Player.SteamUserId);
            }
            else
            {
                // Logika plików XML
                string playerFolder = Path.Combine(Plugin.StoragePath, "EventSystem", "PlayerAccounts");
                string filePath = Path.Combine(playerFolder, $"{steamId}.xml");

                if (!File.Exists(filePath))
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Player account file does not exist for {steamId}.", Color.Red, Context.Player.SteamUserId);
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

                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Modified points for {steamId}. New total: {playerAccount.Points}", Color.Green, Context.Player.SteamUserId);
            }
        }


        [Command("refreshblocks", "Refreshes the list of screens for updates.")]
        [Permission(MyPromoteLevel.Admin)]
        public void RefreshBlocks()
        {
            var monitor = Plugin._activeEventsLCDManager;
            var monitortwo = Plugin._allEventsLcdManager;
            if (monitor != null || monitortwo != null)
            {
                monitor.CacheBlocksForUpdate();
                monitortwo.CacheBlocksForUpdate();
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "Blocks to be updated have been refreshed.", Color.Green, Context.Player.SteamUserId);
            }
            else
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", "Server Load Monitor is not initialized.", Color.Green, Context.Player.SteamUserId);
            }
        }
    }
}
