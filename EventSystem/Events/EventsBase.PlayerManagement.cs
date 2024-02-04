using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventSystem.Events
{
    public abstract partial class EventsBase
    {
        //list of players who joined the event
        protected ConcurrentDictionary<long, bool> ParticipatingPlayers { get; } = new ConcurrentDictionary<long, bool>();

        //Determines whether an event requires that a player not be in another event to join it 
        //True - Does not allow you to join another event
        //False - Allows you to join another event
        public bool AllowParticipationInOtherEvents { get; set; }

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

        public async Task<(bool, string)> CanPlayerJoinEvent(long steamId)
        {
            // Sprawdź, czy gracz już uczestniczy w tym evencie
            if (ParticipatingPlayers.ContainsKey(steamId))
            {
                return (false, "You are already participating in this event.");
            }

            // Sprawdź, czy gracz uczestniczy w innym evencie, jeśli bieżący event nie zezwala na wielokrotne uczestnictwo
            if (!AllowParticipationInOtherEvents)
            {
                var otherEventParticipating = await Task.Run(() =>
                    EventSystemMain.Instance._eventManager.Events
                        .FirstOrDefault(e => e != this && e.IsActiveNow() && e.IsPlayerParticipating(steamId).Result));

                if (otherEventParticipating != null)
                {
                    return (false, $"You are already participating in the event '{otherEventParticipating.EventName}' that does not allow participation in multiple events.");
                }
            }

            // Gracz może dołączyć do eventu
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
        public virtual Task<bool> IsPlayerParticipating(long steamId)
        {
            bool isParticipating = ParticipatingPlayers.ContainsKey(steamId);
            return Task.FromResult(isParticipating);
        }

        // Checks the player's progress in the event.
        public abstract Task CheckPlayerProgress(long steamId);

        // Calculates the time remaining in the event.
        public virtual async Task AwardPlayer(long steamId, long points)
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
    }
}
