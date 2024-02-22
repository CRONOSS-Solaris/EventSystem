using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VRageMath;

namespace EventSystem.Event
{
    public partial class ArenaTeamFight : EventsBase
    {
        public static new readonly Logger Log = LogManager.GetLogger("EventSystem/ArenaTeamFight");
        private readonly EventSystemConfig _config;
        private bool EventStarted = false;
        private Timer _roundTimer;
        public ConcurrentDictionary<int, Team> Teams { get; } = new ConcurrentDictionary<int, Team>();

        public ArenaTeamFight(EventSystemConfig config)
        {
            _config = config;
            EventName = "ArenaTeamFight";
            AllowParticipationInOtherEvents = true;
            UseEventSpecificConfig = false;
            PrefabStoragePath = Path.Combine("EventSystem", "ArenaTeamFightBP");
        }


        public override async Task SystemStartEvent()
        {
            await InitializeArena();

            // Inicjalizacja drużyn
            InitializeTeams();
            RegisterDamageHandler();

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Executing ArenaTeamFight and teams initialized.");
        }

        private void InitializeTeams()
        {
            // Przykładowa inicjalizacja dwóch drużyn
            Teams.TryAdd(1, new Team { Name = _config.ArenaTeamFightSettings.Team1Name, TeamID = 1 });
            Teams.TryAdd(2, new Team { Name = _config.ArenaTeamFightSettings.Team2Name, TeamID = 2 });

            // Logowanie informacji o utworzonych drużynach
            foreach (var team in Teams)
            {
                LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Team initialized: {team.Value.Name} with ID: {team.Value.TeamID}");
            }
        }


        /// <summary>
        /// Asynchronously initializes the arena by spawning the grid and setting up spawn points for teams.
        /// </summary>
        private async Task InitializeArena()
        {
            string gridName = _config.ArenaTeamFightSettings.PrefabName;
            Vector3D position = new Vector3D(
                _config.ArenaTeamFightSettings.SpawnPositionX,
                _config.ArenaTeamFightSettings.SpawnPositionY,
                _config.ArenaTeamFightSettings.SpawnPositionZ);

            var spawnedEntityIds = await SpawnGrid(gridName, position);
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Spawned grid '{gridName}' with entity IDs: {string.Join(", ", spawnedEntityIds)}");
        }


        /// <summary>
        /// Starts the Arena Team Fight event, teleports players, assigns weapons, and subscribes to necessary events.
        /// </summary>
        public override async Task StartEvent()
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Starting ArenaTeamFight Event.");
            EventStarted = true;

            // Dispose of the round timer if it already exists to avoid memory leaks
            _roundTimer?.Dispose();
            // Initialize the round timer to end the round after the configured duration
            _roundTimer = new Timer(EndRoundCallback, null, TimeSpan.FromMinutes(_config.ArenaTeamFightSettings.MatchDurationInMinutes), Timeout.InfiniteTimeSpan);

            // Iterate through each team and prepare each player for the event
            foreach (var teamEntry in Teams)
            {
                var team = teamEntry.Value;
                foreach (var playerId in team.Members.Keys)
                {
                    // Clear the player's inventory before the event starts
                    RemoveAllItemsFromPlayer(playerId);
                    // Teleport the player to their team's specific spawn point
                    TeleportPlayerToSpecificSpawnPoint(playerId, team.TeamID);
                    // Assign a random weapon and ammunition to the player
                    await AssignRandomWeaponAndAmmo(playerId);
                    // Subscribe to character death events for the player
                    SubscribeToCharacterDeath(playerId);
                }
            }
            // Subscribe to update per second to check for kills during the event
            SubscribeToUpdatePerSecond(CheckForKills, priority: 1);

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "ArenaTeamFight event has started.");
        }


        /// <summary>
        /// Handles the callback for ending a round, clears inventories, teleports players back, and awards points to teams.
        /// </summary>
        /// <param name="state">The state object passed to the timer callback.</param>
        private void EndRoundCallback(object state)
        {
            _roundTimer?.Dispose(); // Zatrzymanie timera po zakończeniu rundy.
            Task.Run(async () => await EndRound()).ConfigureAwait(false);
        }

        /// <summary>
        /// Ends the current round of the Arena Team Fight event, awards points to players, and resets the event state for the next round.
        /// </summary>
        private async Task EndRound()
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Round Ending.");
            UnsubscribeFromUpdatePerSecond(CheckForKills);
            ClearAllPlayerInventories();
            TeleportPlayersBack();
            ReturnItemsToPlayers();

            foreach (var teamId in Teams.Keys)
            {
                var team = Teams[teamId];
                int membersCount = team.Members.Count;
                // Zaokrąglanie punktów do góry dzielonych na liczbę członków drużyny
                int pointsPerMember = (int)Math.Ceiling((double)team.KillPoints / membersCount);

                // Przyznawanie punktów każdemu członkowi drużyny
                foreach (var memberId in team.Members.Keys.ToList())
                {
                    await AwardPlayer(memberId, pointsPerMember);
                    UnsubscribeFromCharacterDeath(memberId);
                    // Usunięcie członka drużyny
                    team.Members.TryRemove(memberId, out _);
                }
            }

            ParticipatingPlayers.Clear();
            // Opcjonalnie zresetuj stan eventu, aby przygotować się na kolejną rundę.
            EventStarted = false; // Pozwól na ponowne rozpoczęcie rundy, gdy gracze zaczną dołączać.
        }


        public override async Task SystemEndEvent()
        {
            await EndRound();
            await CleanupGrids();
        }

        /// <summary>
        /// Attempts to add a player to the event, assigning them to a team if possible.
        /// </summary>
        /// <param name="steamId">The Steam ID of the player to add.</param>
        /// <returns>A tuple indicating whether the player was added successfully and a message indicating the result.</returns>
        public override async Task<(bool, string)> AddPlayer(long steamId)
        {
            if (EventStarted)
            {
                return (false, "Event has already started.");
            }

            // Sprawdź, czy gracz już uczestniczy w evencie
            if (ParticipatingPlayers.ContainsKey(steamId))
            {
                return (false, "You are already participating in this event.");
            }

            // Sprawdź, czy gracz uczestniczy w innym evencie, jeśli bieżący event nie pozwala na wielokrotne uczestnictwo
            var (canJoin, errorMessage) = await CanPlayerJoinEvent(steamId);
            if (!canJoin)
            {
                return (canJoin, errorMessage);
            }

            bool playerAdded = false;
            string message = "Could not add player to the team.";

            // Próba dodania gracza do jednej z drużyn, które mają jeszcze wolne miejsca.
            foreach (var team in Teams)
            {
                if (team.Value.Members.Count < _config.ArenaTeamFightSettings.MaxPlayersPerTeam)
                {
                    // Użycie TryAdd zapewnia, że ten sam gracz nie zostanie dodany dwa razy do tej samej drużyny.
                    bool added = team.Value.Members.TryAdd(steamId, true);
                    if (added)
                    {
                        // Dodaj gracza do listy uczestników eventu
                        ParticipatingPlayers.TryAdd(steamId, true);
                        playerAdded = true;
                        message = $"You have successfully joined {team.Value.Name}.";
                        break; 
                    }
                }
            }

            if (!playerAdded)
            {
                // Wszystkie drużyny są pełne lub gracz jest już w jednej z drużyn.
                return (false, "All teams are full or player is already in a team.");
            }
            else
            {
                // Sprawdzenie, czy wszystkie drużyny są pełne po dodaniu nowego gracza.
                if (Teams.All(t => t.Value.Members.Count == _config.ArenaTeamFightSettings.MaxPlayersPerTeam))
                {
                    EventStarted = true;
                    LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "All teams are full. Event starting.");
                    await StartEvent(); //Event Launch
                }
            }

            return (true, message);
        }

        public class Team
        {
            public string Name { get; set; }
            public int TeamID { get; set; }
            public ConcurrentDictionary<long, bool> Members { get; set; } = new ConcurrentDictionary<long, bool>();
            public Vector3D SpawnPoint { get; set; }
            public int KillPoints { get; set; } = 0;
        }

    }
}
