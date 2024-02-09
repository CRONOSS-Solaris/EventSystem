using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var prefabFolderPath = Path.Combine(Plugin.StoragePath, "EventSystem", "CommandPrefabTest");
            var filePath = Path.Combine(prefabFolderPath, gridName + ".sbc");

            Vector3D position = new Vector3D(x, y, z);
            HashSet<long> entityIds = await GridSerializer.LoadAndSpawnGrid(prefabFolderPath, gridName, position);


            if (entityIds.Count > 0)
            {
                string entityIdsString = string.Join(", ", entityIds);
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Grid {gridName} successfully spawned at {position}. Entity IDs: {entityIdsString}", Color.Green, Context.Player.SteamUserId);
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

        [Command("testjoin", "Test joining an event by adding a fictional player.")]
        [Permission(MyPromoteLevel.Admin)]
        public async Task TestJoinEvent(string eventName)
        {
            var eventManager = Plugin._eventManager;
            var eventToJoin = eventManager.Events.FirstOrDefault(e => e.EventName.Equals(eventName, StringComparison.OrdinalIgnoreCase));

            if (eventToJoin != null)
            {
                // Sprawdź, czy event jest aktywny
                if (eventToJoin.IsActiveNow())
                {
                    // Generuj losowy Steam ID
                    long fakeSteamId = GenerateFakeSteamId();

                    // Dodaj fikcyjną osobę do eventu
                    var (success, message) = await eventToJoin.AddPlayer(fakeSteamId);
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", message, success ? Color.Green : Color.Red, Context.Player?.SteamUserId ?? 0);
                }
                else
                {
                    // Event nie jest aktywny
                    EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Event '{eventName}' is not active.", Color.Red, Context.Player?.SteamUserId ?? 0);
                }
            }
            else
            {
                // Event nie istnieje
                EventSystemMain.ChatManager.SendMessageAsOther($"{Plugin.Config.EventPrefix}", $"Event '{eventName}' does not exist.", Color.Red, Context.Player?.SteamUserId ?? 0);
            }
        }

        // Metoda do generowania losowego Steam ID
        private long GenerateFakeSteamId()
        {
            Random random = new Random();
            long fakeSteamId = random.Next(100000000, 999999999);
            return fakeSteamId;
        }

    }
}
