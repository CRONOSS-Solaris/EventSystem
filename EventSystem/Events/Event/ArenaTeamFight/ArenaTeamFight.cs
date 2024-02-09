using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VRageMath;

namespace EventSystem.Event
{
    public class ArenaTeamFight : EventsBase
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
                foreach (var memberId in team.Members.Keys.ToList()) // Używamy ToList(), aby uniknąć błędów podczas modyfikacji kolekcji
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
        /// Clears inventories of all participating players.
        /// </summary>
        private void ClearAllPlayerInventories()
        {
            foreach (var playerId in ParticipatingPlayers.Keys)
            {
                ClearPlayerInventory(playerId);
            }
        }

        /// <summary>
        /// Checks for kills during the event and awards points to attacking teams.
        /// </summary>
        private void CheckForKills()
        {
            foreach (var victim in LastAttackers.Keys)
            {
                if (!ParticipatingPlayers.ContainsKey(victim))
                    continue; // Ignoruj, jeśli ofiara nie bierze udziału w wydarzeniu

                long attacker;
                if (LastAttackers.TryRemove(victim, out attacker) && ParticipatingPlayers.ContainsKey(attacker))
                {
                    // Sprawdź, czy ofiara i atakujący są z różnych drużyn
                    var victimTeam = Teams.Values.FirstOrDefault(team => team.Members.ContainsKey(victim));
                    var attackerTeam = Teams.Values.FirstOrDefault(team => team.Members.ContainsKey(attacker));

                    if (victimTeam != null && attackerTeam != null && victimTeam != attackerTeam)
                    {
                        // Przyznaj punkty drużynie atakującego
                        attackerTeam.KillPoints += _config.ArenaTeamFightSettings.PointsPerKill;

                        LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Player {attacker} zabił gracza {victim}. Drużyna {attackerTeam.Name} zdobywa {_config.ArenaTeamFightSettings.PointsPerKill} punktów.");

                        // Wywołanie respawnu dla ofiary
                        Task.Run(() => RespawnPlayer(victim, victimTeam));
                    }
                }
            }
        }

        /// <summary>
        /// Respawns a player after being killed during the event.
        /// </summary>
        /// <param name="playerId">The ID of the player to respawn.</param>
        /// <param name="team">The team to which the player belongs.</param>
        private async Task RespawnPlayer(long playerId, Team team)
        {
            // Użyj właściwości TeamID z obiektu team do identyfikacji drużyny
            TeleportPlayerToSpecificSpawnPoint(playerId, team.TeamID);

            // Przydzielenie ekwipunku
            await AssignRandomWeaponAndAmmo(playerId);
        }

        /// <summary>
        /// Assigns a random weapon and corresponding ammunition to a player.
        /// </summary>
        /// <param name="playerId">The ID of the player to assign the weapon and ammunition to.</param>
        private async Task AssignRandomWeaponAndAmmo(long playerId)
        {
            // Zdefiniuj listę dostępnych broni i odpowiadających im typów amunicji
            var weaponsWithAmmo = new Dictionary<string, (string weaponSubtypeID, int ammoQuantity)>() {
                 // Przykładowe dane
                {"Rifle", ("RifleAmmo", 120)},
                {"Pistol", ("PistolAmmo", 50)}
            };
    
            // Losowe wybranie broni
            var random = new Random();
            var selectedWeapon = weaponsWithAmmo.ElementAt(random.Next(weaponsWithAmmo.Count));

            // Dodanie broni do ekwipunku gracza
            AddItemToPlayer(playerId, "MyObjectBuilder_Weapon", selectedWeapon.Key, 1);

            // Dodanie amunicji do ekwipunku gracza
            AddItemToPlayer(playerId, "MyObjectBuilder_AmmoMagazine", selectedWeapon.Value.weaponSubtypeID, selectedWeapon.Value.ammoQuantity);
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

        /// <summary>
        /// Teleports a player to a specific spawn point based on their team ID.
        /// </summary>
        /// <param name="playerId">The ID of the player to teleport.</param>
        /// <param name="teamId">The ID of the team the player belongs to.</param>
        public void TeleportPlayerToSpecificSpawnPoint(long playerId, int teamId)
        {
            if (!Teams.TryGetValue(teamId, out Team team))
            {
                Log.Error($"Could not find team {teamId}.");
                return;
            }

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Attempting to find spawn block for team {teamId}.");
            string spawnBlockName = teamId == 1 ? _config.ArenaTeamFightSettings.BlockSpawn1Name : _config.ArenaTeamFightSettings.BlockSpawn2Name;

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                var spawnPoint = FindBlockPositionByName(spawnBlockName);
                if (spawnPoint.HasValue)
                {
                    TeleportPlayerToSpawnPoint(playerId, spawnPoint.Value);
                    LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Teleporting player {playerId} to spawn point for team {teamId} at {spawnPoint.Value}.");
                }
                else
                {
                    Log.Error($"Could not find spawn block '{spawnBlockName}' for team {teamId}.");
                }
            });
        }



        /// <summary>
        /// Loads settings for the Arena Team Fight event from the configuration file.
        /// </summary>
        /// <param name="config">The configuration object containing event settings.</param>
        public override Task LoadEventSettings(EventSystemConfig config)
        {
            if (config.ArenaTeamFightSettings == null)
            {
                config.ArenaTeamFightSettings = new ArenaTeamFightConfig
                {
                    IsEnabled = false,
                    ActiveDaysOfMonth = new List<int> { 1, 15, 20 },
                    StartTime = "00:00:00",
                    EndTime = "23:59:59",
                    PrefabName = "arenapvp",
                    SpawnPositionX = -42596.88,
                    SpawnPositionY = 40764.17,
                    SpawnPositionZ = -16674.06,
                    Team1Name = "Red",
                    BlockSpawn1Name = "SpawnPointTeam1",
                    Team2Name = "Blue",
                    BlockSpawn2Name = "SpawnPointTeam2",
                    MaxPlayersPerTeam = 10,
                    MatchDurationInMinutes = 5,
                    PointsPerKill = 10
                };
            }

            var settings = config.ArenaTeamFightSettings;
            IsEnabled = settings.IsEnabled;
            ActiveDaysOfMonth = settings.ActiveDaysOfMonth;
            StartTime = TimeSpan.Parse(settings.StartTime);
            EndTime = TimeSpan.Parse(settings.EndTime);

            // Ustawienie pozycji spawnu na podstawie konfiguracji
            Vector3D spawnPosition = new Vector3D(settings.SpawnPositionX, settings.SpawnPositionY, settings.SpawnPositionZ);

            string activeDaysText = ActiveDaysOfMonth.Count > 0 ? string.Join(", ", ActiveDaysOfMonth) : "Every day";
            LoggerHelper.DebugLog(Log, _config, $"Loaded ArenaTeamFight settings: IsEnabled={IsEnabled}, Active Days of Month={activeDaysText}, StartTime={StartTime}, EndTime={EndTime}, SpawnPosition={spawnPosition}");

            return Task.CompletedTask;
        }

        public class Team
        {
            public string Name { get; set; }
            public int TeamID { get; set; }
            public ConcurrentDictionary<long, bool> Members { get; set; } = new ConcurrentDictionary<long, bool>();
            public Vector3D SpawnPoint { get; set; }
            public int KillPoints { get; set; } = 0;
        }


        public class ArenaTeamFightConfig
        {
            public bool IsEnabled { get; set; }
            public List<int> ActiveDaysOfMonth { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public string PrefabName { get; set; }
            public double SpawnPositionX { get; set; }
            public double SpawnPositionY { get; set; }
            public double SpawnPositionZ { get; set; }
            public string Team1Name { get; set; }
            public string BlockSpawn1Name { get; set; }
            public string Team2Name { get; set; }
            public string BlockSpawn2Name { get; set; }
            public int MaxPlayersPerTeam { get; set; }
            public int MatchDurationInMinutes { get; set; }
            public int PointsPerKill { get; set; }
        }

    }
}
