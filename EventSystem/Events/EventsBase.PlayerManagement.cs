using EventSystem.Utils;
using Sandbox.ModAPI;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.ObjectBuilders;
using VRageMath;

namespace EventSystem.Events
{
    public abstract partial class EventsBase
    {
        //list of players who joined the event
        protected ConcurrentDictionary<long, bool> ParticipatingPlayers { get; } = new ConcurrentDictionary<long, bool>();

        // Dictionary to store original player positions before teleportation
        protected ConcurrentDictionary<long, Vector3D> originalPlayerPositions = new ConcurrentDictionary<long, Vector3D>();

        // Stores information about items taken from players (SteamID, Item List).
        protected ConcurrentDictionary<long, Dictionary<MyDefinitionId, MyFixedPoint>> _itemsRemovedFromPlayers = new ConcurrentDictionary<long, Dictionary<MyDefinitionId, MyFixedPoint>>();

        //Determines whether an event requires that a player not be in another event to join it 
        //True -  Allows you to join another event
        //False - Does not allow you to join another event
        protected bool AllowParticipationInOtherEvents { get; set; }

        // Adds the player to the list of event participants.
        public virtual async Task<(bool, string)> AddPlayer(long steamId)
        {
            // Check if the player is already participating in the event
            if (ParticipatingPlayers.ContainsKey(steamId))
            {
                return (false, "You are already participating in this event.");
            }

            // If the event does not allow participation in other events, check if the player is already participating in another event
            var (canJoin, message) = await CanPlayerJoinEvent(steamId);
            if (!canJoin)
            {
                return (canJoin, message);
            }

            // Add the player to the list of participants
            var added = ParticipatingPlayers.TryAdd(steamId, true);
            if (added)
            {
                return (true, $"You have successfully joined the event: {EventName}.");
            }
            else
            {
                return (false, "An error occurred. Please try again.");
            }
        }

        // Check if the player is already participating in an event and if he allows to join another one
        public async Task<(bool, string)> CanPlayerJoinEvent(long steamId)
        {
            // Check if the player is already participating in this event
            if (ParticipatingPlayers.ContainsKey(steamId))
            {
                return (false, "You are already participating in this event.");
            }

            // Retrieve all events in which the player is currently participating
            var participatingEvents = await Task.Run(() =>
                EventSystemMain.Instance._eventManager.Events
                    .Where(e => e.IsPlayerParticipating(steamId).Result)
                    .Select(e => e.EventName)
                    .ToList());

            // If participating in any events
            if (participatingEvents.Any())
            {
                // If not allowed to participate in other events
                if (!AllowParticipationInOtherEvents)
                {
                    var eventNames = string.Join(", ", participatingEvents);
                    return (false, $"You cannot join this event because you are already participating in other event(s): {eventNames}.");
                }

                // If other events do not allow participation in multiple events
                var otherEventDisallowing = participatingEvents.FirstOrDefault(e =>
                    EventSystemMain.Instance._eventManager.Events
                        .FirstOrDefault(ev => ev.EventName == e && !ev.AllowParticipationInOtherEvents) != null);

                if (otherEventDisallowing != null)
                {
                    return (false, $"You are already participating in the event '{otherEventDisallowing}' that does not allow participation in multiple events.");
                }
            }

            // Player can join the event
            return (true, "");
        }


        // Removes the player from the list of event participants.
        public virtual async Task<(bool, string)> LeavePlayer(long steamId)
        {
            bool removed = ParticipatingPlayers.TryRemove(steamId, out _);
            if (removed)
            {
                return (true, $"You have successfully left the event: {EventName}.");
            }
            else
            {
                return (false, "You were not participating in this event or an error occurred.");
            }
        }

        // Checks if the player is in the list of event participants.
        protected virtual Task<bool> IsPlayerParticipating(long steamId)
        {
            bool isParticipating = ParticipatingPlayers.ContainsKey(steamId);
            return Task.FromResult(isParticipating);
        }

        // Checks the player's progress in the event.
        public abstract Task CheckPlayerProgress(long steamId);

        // Calculates the time remaining in the event.
        protected virtual async Task AwardPlayer(long steamId, long points)
        {
            // Get the database manager from the main plugin
            var databaseManager = EventSystemMain.Instance.DatabaseManager;

            // Get the configuration
            var config = EventSystemMain.Instance.Config;

            if (config.UseDatabase)
            {
                // Reward logic in the database
                await databaseManager.UpdatePlayerPointsAsync(steamId.ToString(), points);
            }
            else
            {
                // Get the player account manager from the main plugin
                var xmlManager = EventSystemMain.Instance.PlayerAccountXmlManager;
                bool updateResult = await xmlManager.UpdatePlayerPointsAsync(steamId, points).ConfigureAwait(false);
                if (!updateResult)
                {
                    // Handling the situation when the player account does not exist (if necessary).
                }
            }
        }

        public virtual int GetParticipantsCount()
        {
            return ParticipatingPlayers.Count;
        }

        // Methods of teleporting the player to the event's sleeping position
        // Methods of teleporting the player to the event's spawn point
        protected async Task TeleportPlayerToSpawnPoint(long steamId, Vector3D spawnPoint)
        {
            if (!Utilities.TryGetPlayerBySteamId(steamId, out IMyPlayer player) || player.Character == null)
            {
                Log.Error($"Player with SteamID {steamId} not found or does not have a character.");
                return;
            }

            // Save the player's original position
            originalPlayerPositions[steamId] = player.GetPosition();

            // Find a free space near spawnPoint
            Vector3D? targetPos = MyAPIGateway.Entities.FindFreePlace(spawnPoint, 2.0f); // Example search radius

            if (!targetPos.HasValue)
            {
                Log.Error($"No free place found near the spawn point for player {player.DisplayName}.");
                return;
            }

            // Perform teleportation to the free space found
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                player.Character.SetPosition(targetPos.Value);
                Log.Info($"Teleported player {player.DisplayName} to free place near spawn point at {targetPos.Value}.");
            });
        }

        // Method to teleport players back to their original positions
        protected void TeleportPlayersBack()
        {
            foreach (var kvp in originalPlayerPositions)
            {
                long steamId = kvp.Key;
                Vector3D originalPosition = kvp.Value;

                if (!Utilities.TryGetPlayerBySteamId(steamId, out IMyPlayer player) || player.Character == null)
                {
                    Log.Error($"Player with SteamID {steamId} not found or does not have a character.");
                    continue;
                }

                // Perform teleportation in the main game thread
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    player.Character.SetPosition(originalPosition);
                    Log.Info($"Teleported player {player.DisplayName} back to original position at {originalPosition}.");
                });
            }

            // Clear stored positions
            originalPlayerPositions.Clear();
        }


        protected bool RemoveAllItemsFromPlayer(long steamId)
        {
            if (!Utilities.TryGetPlayerBySteamId(steamId, out IMyPlayer player) || player.Character == null)
            {
                Log.Error($"Player with SteamID {steamId} not found or does not have a character.");
                return false;
            }

            var inventory = player.Character.GetInventory() as VRage.Game.ModAPI.IMyInventory;
            if (inventory == null)
            {
                Log.Error($"Could not get the inventory for player with SteamID {steamId}.");
                return false;
            }

            // Zapisz obecny stan przedmiotów w ekwipunku przed jego wyczyszczeniem
            List<MyInventoryItem> itemsBeforeClear = new List<MyInventoryItem>();
            inventory.GetItems(itemsBeforeClear);

            // Słownik do przechowywania usuniętych przedmiotów i ich ilości
            Dictionary<MyDefinitionId, MyFixedPoint> removedItemsDetails = new Dictionary<MyDefinitionId, MyFixedPoint>();

            // Usuń wszystkie przedmioty z ekwipunku
            foreach (var item in itemsBeforeClear)
            {
                var definitionId = MyDefinitionId.Parse($"{item.Type.TypeId}/{item.Type.SubtypeId}");
                inventory.RemoveItemsOfType(item.Amount, definitionId, spawn: false);
                if (removedItemsDetails.ContainsKey(definitionId))
                {
                    removedItemsDetails[definitionId] += item.Amount; // Dodaj ilość, jeśli przedmiot już istnieje
                }
                else
                {
                    removedItemsDetails.Add(definitionId, item.Amount); // Dodaj nowy wpis, jeśli przedmiotu nie ma na liście
                }
            }

            // Zapisz informacje o usuniętych przedmiotach, aby móc je zwrócić później
            if (removedItemsDetails.Count > 0)
            {
                _itemsRemovedFromPlayers.AddOrUpdate(steamId, removedItemsDetails, (key, existingDict) =>
                {
                    foreach (var item in removedItemsDetails)
                    {
                        if (existingDict.ContainsKey(item.Key))
                        {
                            existingDict[item.Key] += item.Value;
                        }
                        else
                        {
                            existingDict.Add(item.Key, item.Value);
                        }
                    }
                    return existingDict;
                });
            }

            Log.Info($"Removed all items from player with SteamID {steamId}.");
            return true;
        }

        protected bool AddItemToPlayer(long steamId, string typeID, string subtypeID, int quantity)
        {
            if (!Utilities.TryGetPlayerBySteamId(steamId, out IMyPlayer player) || player.Character == null)
            {
                Log.Error($"Player with SteamID {steamId} not found or does not have a character.");
                return false;
            }

            MyDefinitionId definitionId = new MyDefinitionId(MyObjectBuilderType.Parse(typeID), subtypeID);

            var inventory = player.Character.GetInventory();

            MyObjectBuilder_PhysicalObject ob = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(definitionId);
            inventory.AddItems(quantity, ob);

            return true;
        }

        protected void ReturnItemsToPlayers()
        {
            foreach (var entry in _itemsRemovedFromPlayers)
            {
                ulong steamId = (ulong)entry.Key;
                var itemsToReturn = entry.Value;

                foreach (var definitionId in itemsToReturn.Keys)
                {
                    // Get the quantity of the item
                    int quantity = (int)itemsToReturn[definitionId];

                    // Default to adding one unit of each item
                    AddItemToPlayer((long)steamId, definitionId.SubtypeId.ToString(), definitionId.SubtypeId.ToString(), quantity);
                }
            }

            // Clear the list after returning items
            _itemsRemovedFromPlayers.Clear();
        }

    }
}
