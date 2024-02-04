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
        //True -  Allows you to join another event
        //False - Does not allow you to join another event
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
