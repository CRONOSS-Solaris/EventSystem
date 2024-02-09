using EventSystem.Utils;
using Sandbox.ModAPI;
using System;
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

        protected ConcurrentDictionary<long, long> LastAttackers = new ConcurrentDictionary<long, long>();


        // Definition of a public event that can be triggered when a character dies
        protected event Func<IMyCharacter, Task> CharacterDeath;

        //Determines whether an event requires that a player not be in another event to join it 
        //True -  Allows you to join another event
        //False - Does not allow you to join another event
        protected bool AllowParticipationInOtherEvents { get; set; }

        /// <summary>
        /// Adds the player to the list of event participants.
        /// </summary>
        /// <param name="steamId">The SteamID of the player to be added.</param>
        /// <returns>A tuple indicating success (true/false) and an associated message.</returns>
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

        /// <summary>
        /// Checks if the player is already participating in an event and if they are allowed to join another one.
        /// </summary>
        /// <param name="steamId">The SteamID of the player to check.</param>
        /// <returns>A tuple indicating whether the player can join (true/false) and a message.</returns>
        protected async Task<(bool, string)> CanPlayerJoinEvent(long steamId)
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


        /// <summary>
        /// Removes the player from the list of event participants.
        /// </summary>
        /// <param name="steamId">The SteamID of the player to be removed.</param>
        /// <returns>A tuple indicating success (true/false) and an associated message.</returns>
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

        /// <summary>
        /// Checks if the player is currently participating in the event.
        /// </summary>
        /// <param name="steamId">The SteamID of the player to check.</param>
        /// <returns>A task returning true if the player is participating; otherwise, false.</returns>
        public virtual Task<bool> IsPlayerParticipating(long steamId)
        {
            bool isParticipating = ParticipatingPlayers.ContainsKey(steamId);
            return Task.FromResult(isParticipating);
        }

        /// <summary>
        /// Checks the player's progress in the event.
        /// </summary>
        /// <param name="steamId">The SteamID of the player whose progress is checked.</param>
        public virtual Task CheckPlayerProgress(long steamId)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Awards points to a player for their performance in the event.
        /// </summary>
        /// <param name="steamId">The SteamID of the player to award points to.</param>
        /// <param name="points">The number of points to award.</param>
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

        /// <summary>
        /// Returns the count of participants in the event.
        /// </summary>
        /// <returns>The number of participants.</returns>
        public virtual int GetParticipantsCount()
        {
            return ParticipatingPlayers.Count;
        }

        /// <summary>
        /// Teleports a player to the event's spawn point.
        /// </summary>
        /// <param name="steamId">The SteamID of the player to teleport.</param>
        /// <param name="spawnPoint">The spawn point where the player should be teleported.</param>
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
                LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Teleported player {player.DisplayName} to free place near spawn point at {targetPos.Value}.");
            });
        }

        /// <summary>
        /// Teleports players back to their original positions after an event.
        /// </summary>
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
                    LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Teleported player {player.DisplayName} back to original position at {originalPosition}.");
                });
            }

            // Clear stored positions
            originalPlayerPositions.Clear();
        }

        /// <summary>
        /// Removes all items from a player's inventory.
        /// </summary>
        /// <param name="steamId">The SteamID of the player whose items are to be removed.</param>
        /// <returns>True if items were successfully removed; otherwise, false.</returns>
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

            // Save the current state of the items in the inventory before clearing it
            List<MyInventoryItem> itemsBeforeClear = new List<MyInventoryItem>();
            inventory.GetItems(itemsBeforeClear);

            // Save the current state of the items in the inventory before clearing it
            Dictionary<MyDefinitionId, MyFixedPoint> removedItemsDetails = new Dictionary<MyDefinitionId, MyFixedPoint>();

            // Remove all items from the inventory
            foreach (var item in itemsBeforeClear)
            {
                var definitionId = MyDefinitionId.Parse($"{item.Type.TypeId}/{item.Type.SubtypeId}");
                inventory.RemoveItemsOfType(item.Amount, definitionId, spawn: false);
                if (removedItemsDetails.ContainsKey(definitionId))
                {
                    removedItemsDetails[definitionId] += item.Amount; // Add the quantity if the item already exists
                }
                else
                {
                    removedItemsDetails.Add(definitionId, item.Amount); // Add a new entry if the item is not listed
                }
            }

            // Save the information about the deleted items so that they can be returned later
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

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Removed all items from player with SteamID {steamId}.");
            return true;
        }

        /// <summary>
        /// Adds specified items to a player's inventory.
        /// </summary>
        /// <param name="steamId">The SteamID of the player to add items to.</param>
        /// <param name="typeID">The type ID of the item to add.</param>
        /// <param name="subtypeID">The subtype ID of the item to add.</param>
        /// <param name="quantity">The quantity of items to add.</param>
        /// <returns>True if items were successfully added; otherwise, false.</returns>
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

        /// <summary>
        /// Returns removed items to their respective players, e.g., at the end of an event after teleporting them to their previous positions.
        /// </summary>
        protected void ReturnItemsToPlayers()
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "ReturnItemsToPlayers: Starting to return items to players.");
            foreach (var entry in _itemsRemovedFromPlayers)
            {
                ulong steamId = (ulong)entry.Key;
                var itemsToReturn = entry.Value;

                LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"ReturnItemsToPlayers: Processing player with SteamID {steamId}.");

                foreach (var definitionId in itemsToReturn.Keys)
                {
                    // Get the quantity of the item
                    int quantity = (int)itemsToReturn[definitionId];

                    // Log the item and quantity being returned
                    LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"ReturnItemsToPlayers: Returning {quantity} of {definitionId} to player.");

                    // Default to adding one unit of each item
                    AddItemToPlayer((long)steamId, definitionId.SubtypeId.ToString(), definitionId.SubtypeId.ToString(), quantity);
                }
            }

            // Clear the list after returning items
            _itemsRemovedFromPlayers.Clear();
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "ReturnItemsToPlayers: Completed returning items to players.");
        }


        /// <summary>
        /// Clears a player's inventory without saving its contents.
        /// </summary>
        /// <param name="steamId">The SteamID of the player whose inventory should be cleared.</param>
        protected void ClearPlayerInventory(long steamId)
        {
            if (Utilities.TryGetPlayerBySteamId(steamId, out IMyPlayer player) && player.Character != null)
            {
                var inventory = player.Character.GetInventory();
                if (inventory != null)
                {
                    inventory.Clear();
                    LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Cleared inventory for player with SteamID {steamId}.");
                }
            }
            else
            {
                Log.Error($"Player with SteamID {steamId} not found or does not have a character.");
            }
        }

        /// <summary>
        /// Finds a character by their SteamID.
        /// </summary>
        /// <param name="steamId">The SteamID of the player whose character is to be found.</param>
        /// <returns>The character object as IMyCharacter or null if not found.</returns>
        protected IMyCharacter FindCharacterBySteamId(long steamId)
        {
            if (Utilities.TryGetPlayerBySteamId(steamId, out IMyPlayer player))
            {
                if (player.Character != null)
                {
                    return player.Character as IMyCharacter;
                }
            }
            return null;
        }

        /// <summary>
        /// Subscribes to the character death event for a given player.
        /// </summary>
        /// <param name="steamId">The SteamID of the player for whom the character death event should be subscribed.</param>
        protected void SubscribeToCharacterDeath(long steamId)
        {
            var character = FindCharacterBySteamId(steamId);
            if (character != null)
            {
                character.CharacterDied += OnCharacterDeathHandler;
            }
        }

        /// <summary>
        /// Handler for when a character dies. This method triggers the CharacterDeath event if there are any subscribers.
        /// </summary>
        /// <param name="character">The character that died.</param>
        private void OnCharacterDeathHandler(IMyCharacter character)
        {
            // Wywołanie zdarzenia CharacterDeath, jeśli jest subskrybent
            CharacterDeath?.Invoke(character);
        }

        /// <summary>
        /// A virtual method intended to be overridden in derived classes to handle a character's death. 
        /// </summary>
        /// <param name="character">The character that died.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        protected virtual async Task OnCharacterDeath(IMyCharacter character)
        {

        }

        /// <summary>
        /// Unsubscribes from the character death event for a given player.
        /// </summary>
        /// <param name="steamId">The SteamID of the player for whom the character death event should be unsubscribed.</param>
        protected void UnsubscribeFromCharacterDeath(long steamId)
        {
            var character = FindCharacterBySteamId(steamId);
            if (character != null)
            {
                character.CharacterDied -= OnCharacterDeathHandler;
            }
        }

        /// <summary>
        /// Updates the last attacker for a given victim. This method should be called whenever a player is attacked.
        /// It stores the SteamID of the last player or entity that attacked the victim. This information can be used
        /// to determine the killer when a player dies, allowing for proper attribution of kills in events.
        /// </summary>
        /// <param name="victimSteamId">The SteamID of the player who was attacked.</param>
        /// <param name="attackerSteamId">The SteamID of the player or entity that attacked the victim.</param>
        protected void OnPlayerAttacked(long victimSteamId, long attackerSteamId)
        {
            LastAttackers[victimSteamId] = attackerSteamId;
        }

    }
}
