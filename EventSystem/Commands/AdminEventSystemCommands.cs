using Sandbox.Game.World;
using System.IO;
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
                bool updateResult = await Plugin.DatabaseManager.UpdatePlayerPointsAsync(steamId.ToString(), points);
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

        //Test commands

        [Command("spawngird", "Loads and spawns a grid from a specified file in the 'prefab' folder at a specified position.")]
        [Permission(MyPromoteLevel.Admin)]
        public async Task SpawnGrid(string gridName, double x, double y, double z)
        {
            // Użyj FileManager do uzyskania ścieżki do folderu "prefab"
            var prefabFolderPath = Path.Combine(Plugin.StoragePath, "EventSystem", "CommandPrefabTest");

            // Utwórz pełną ścieżkę do pliku siatki
            var filePath = Path.Combine(prefabFolderPath, gridName + ".sbc");

            Vector3D position = new Vector3D(x, y, z);
            bool result = await GridSerializer.LoadAndSpawnGrid(prefabFolderPath, gridName, position);

            if (result)
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Grid {gridName} successfully spawned at {position}.", Color.Green, Context.Player.SteamUserId);
            }
            else
            {
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Failed to spawn grid {gridName} at {position}.", Color.Red, Context.Player.SteamUserId);
            }
        }

        [Command("listnpcs", "Displays information about all NPCs on the server.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ListNpcs()
        {
            var npcIdentities = MySession.Static.Players.GetNPCIdentities();
            if (npcIdentities.Count == 0)
            {
                Context.Respond("No NPCs found on the server.");
                return;
            }

            foreach (var npcId in npcIdentities)
            {
                var npc = MySession.Static.Players.TryGetIdentity(npcId);
                if (npc != null)
                {
                    Context.Respond($"NPC Name: {npc.DisplayName}, IdentityId: {npc.IdentityId}");
                }
            }
        }
    }
}
