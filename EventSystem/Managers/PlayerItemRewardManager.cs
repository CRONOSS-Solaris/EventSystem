using EventSystem.Utils;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Torch;
using VRage;
using VRage.Game;
using VRage.ObjectBuilders;

namespace EventSystem.Managers
{
    public class PlayerItemRewardManager
    {
        public static bool AwardPlayer(ulong steamId, IRewardItem rewardItem, int randomAmount, Logger log, EventSystemConfig config)
        {
            var player = MySession.Static.Players.TryGetPlayerBySteamId(steamId);
            if (player == null)
            {
                LoggerHelper.DebugLog(log, config, $"PLAYER(): Player with SteamID {steamId} not found.");
                return false;
            }

            MyDefinitionId definitionId = new MyDefinitionId(MyObjectBuilderType.Parse(rewardItem.ItemTypeId), rewardItem.ItemSubtypeId);
            if (!MyDefinitionManager.Static.TryGetPhysicalItemDefinition(definitionId, out var itemDefinition))
            {
                LoggerHelper.DebugLog(log, config, $"ITEM(): Could not find item definition for {rewardItem.ItemTypeId} {rewardItem.ItemSubtypeId}");
                return false;
            }

            var character = player.Character;
            if (character == null)
            {
                LoggerHelper.DebugLog(log, config, $"PLAYER(): Player {player.DisplayName} is not spawned.");
                return false;
            }

            var inventory = character.GetInventory();
            if (inventory == null)
            {
                LoggerHelper.DebugLog(log, config, $"PLAYER(): Could not get the inventory for player {player.DisplayName}.");
                return false;
            }

            var itemVolume = (MyFixedPoint)(randomAmount * itemDefinition.Volume);
            if (inventory.CurrentVolume + itemVolume > inventory.MaxVolume)
            {
                LoggerHelper.DebugLog(log, config, $"INVENTORY(): Not enough space in inventory for player {player.DisplayName}.");
                return false; // Not enough space in inventory
            }

            MyObjectBuilder_PhysicalObject physicalObject = MyObjectBuilderSerializer.CreateNewObject(definitionId) as MyObjectBuilder_PhysicalObject;
            inventory.AddItems(randomAmount, physicalObject);
            return true; // The reward has been awarded
        }
    }
}
