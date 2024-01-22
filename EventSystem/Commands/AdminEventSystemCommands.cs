using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRageMath;

namespace EventSystem
{
    [Category("eventAdmin")]
    public class AdminEventSystemCommands : CommandModule
    {
        public EventSystemMain Plugin => (EventSystemMain)Context.Plugin;

        [Command("modifypoints", "Add or remove points from a player's account.")]
        [Permission(MyPromoteLevel.Admin)]
        public async Task ModifyPoints(ulong steamId, long points)
        {
            if (Plugin.Config.UseDatabase)
            {
                // Logika używania bazy danych
                bool updateResult = Plugin.DatabaseManager.UpdatePlayerPoints(steamId.ToString(), points);
                if (updateResult)
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Modified points for {steamId}.", Color.Green, Context.Player.SteamUserId);
                }
                else
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"No account found for {steamId}.", Color.Red, Context.Player.SteamUserId);
                }
            }
            else
            {
                // Logika używająca PlayerAccountXmlManager
                bool updateResult = await Plugin.PlayerAccountXmlManager.UpdatePlayerPointsAsync((long)steamId, points).ConfigureAwait(false);
                if (updateResult)
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Modified points for {steamId}.", Color.Green, Context.Player.SteamUserId);
                }
                else
                {
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Player account file does not exist for {steamId}.", Color.Red, Context.Player.SteamUserId);
                }
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
