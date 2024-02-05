using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VRageMath;

namespace EventSystem.Event
{
    public class ArenaTeamFight : EventsBase
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/ArenaTeamFight");
        private readonly EventSystemConfig _config;
        private bool EventStarted = false; 
        public ConcurrentDictionary<int, Team> Teams { get; } = new ConcurrentDictionary<int, Team>();

        public ArenaTeamFight(EventSystemConfig config)
        {
            _config = config;
            EventName = "ArenaTeamFight";
            AllowParticipationInOtherEvents = true;
            PrefabStoragePath = Path.Combine("EventSystem", "ArenaTeamFightBP");
        }


        public override async Task SystemStartEvent()
        {
            await InitializeArena();
            string team1Name = _config.ArenaTeamFightSettings.Team1Name ?? "Team 1";
            string team2Name = _config.ArenaTeamFightSettings.Team2Name ?? "Team 2";

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Executing ArenaTeamFight.");

        }

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

        public override async Task StartEvent()
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Starting ArenaTeamFight Event.");

            foreach (var teamId in Teams.Keys)
            {
                var team = Teams[teamId];
                foreach (var playerId in team.Members.Keys)
                {
                    await TeleportPlayerToSpecificSpawnPoint(playerId, teamId);
                }
            }
        }

        public override async Task SystemEndEvent()
        {
            // Implementacja logiki końca wydarzenia
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Ending ArenaTeamFight.");
            await CleanupGrids();
        }

        public override Task CheckPlayerProgress(long steamId)
        {
            return Task.CompletedTask;
        }

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
                        break; // Przerwanie pętli po pomyślnym dodaniu gracza do drużyny.
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

        public async Task TeleportPlayerToSpecificSpawnPoint(long playerId, int teamId)
        {
            if (!Teams.TryGetValue(teamId, out Team team))
            {
                Log.Error($"Could not find team {teamId}.");
                return;
            }

            // Określenie nazwy bloku na podstawie ID drużyny
            string spawnBlockName = teamId == 1 ? _config.ArenaTeamFightSettings.BlockSpawn1Name : _config.ArenaTeamFightSettings.BlockSpawn2Name;

            // Wykorzystanie metody FindBlockPositionByName do znalezienia pozycji bloku
            var spawnPoint = await FindBlockPositionByName(spawnBlockName);

            if (spawnPoint.HasValue)
            {
                // Teleportacja gracza do znalezionej pozycji
                await TeleportPlayerToSpawnPoint(playerId, spawnPoint.Value);
                Log.Info($"Teleporting player {playerId} to spawn point for team {teamId} at {spawnPoint.Value}.");
            }
            else
            {
                Log.Error($"Could not find spawn block '{spawnBlockName}' for team {teamId}.");
            }
        }


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
                    PointsForWinningTeam = 100,
                    PointsForLosingTeam = 10,
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
            public ConcurrentDictionary<long, bool> Members { get; set; } = new ConcurrentDictionary<long, bool>();
            public Vector3D SpawnPoint { get; set; }
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
            public long PointsForWinningTeam { get; set; }
            public long PointsForLosingTeam { get; set; }
        }

    }
}
