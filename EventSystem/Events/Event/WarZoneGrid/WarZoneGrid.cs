using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VRage;
using VRage.Game.ObjectBuilders.Components;
using VRageMath;

namespace EventSystem.Event
{
    public class WarZoneGrid : EventsBase
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/WarZoneGrid");
        private readonly EventSystemConfig _config;
        private ConcurrentDictionary<long, bool> playersInSphere = new ConcurrentDictionary<long, bool>();
        private ConcurrentDictionary<long, bool> playerMessageSent = new ConcurrentDictionary<long, bool>();
        private ConcurrentDictionary<long, DateTime> lastPointsAwarded = new ConcurrentDictionary<long, DateTime>();
        private ConcurrentDictionary<long, DateTime> playerEntryTime = new ConcurrentDictionary<long, DateTime>();
        private ConcurrentDictionary<long, bool> safezoneEntityIds = new ConcurrentDictionary<long, bool>();

        private HashSet<long> currentEnemiesInZone = new HashSet<long>();
        private System.Timers.Timer messageAndGpsTimer;


        private bool enemyInZone = false;


        private Vector3D sphereCenter;
        private double ZoneRadius;


        public WarZoneGrid(EventSystemConfig config)
        {
            _config = config;
            LoadEventSettings(config);
            EventName = _config.WarZoneGridSettings.EventName;
            AllowParticipationInOtherEvents = false;
            UseEventSpecificConfig = false;
            PrefabStoragePath = Path.Combine("EventSystem", "EventPrefabBlueprint");
        }

        public override string EventDescription => _config.WarZoneGridSettings.EventDescription;


        public override async Task SystemStartEvent()
        {
            // Dodanie wszystkich graczy do listy uczestników
            foreach (var player in MySession.Static.Players.GetOnlinePlayers()?.ToList() ?? new List<MyPlayer>())
            {
                long playerId = player.Identity.IdentityId;
                ParticipatingPlayers.TryAdd(playerId, true);
            }

            var settings = _config.WarZoneGridSettings;

            // Losowanie pozycji sfery
            Vector3D initialSphereCenter = RandomizePosition(settings);
            double Radius = settings.Radius;

            // Ustawienia siatki
            var gridSettings = new GridSpawnSettings
            {
                FunctionalBlockSettings = new FunctionalBlockSettings
                {
                    Enabled = true
                },
                CubeGridSettings = new CubeGridSettings
                {
                    DampenersEnabled = true,
                    Editable = false,
                    IsStatic = true,
                    IsPowered = true,
                },
                GridOwnerSettings = new GridOwnerSettings
                {
                    OwnerGrid = settings.OwnerGrid,
                }
            };

            // Definicja nazwy siatki i jej ustawień
            var gridName = settings.PrefabName;
            GridSettingsDictionary[gridName] = gridSettings;

            bool isSpawnSuccessful = false;
            const int maxSpawnAttempts = 3;

            for (int attempt = 1; attempt <= maxSpawnAttempts && !isSpawnSuccessful; attempt++)
            {
                // Spawnowanie siatki na środku strefy.
                var spawnedEntityIds = await SpawnGrid(gridName, initialSphereCenter);

                // Sprawdź, czy siatka została pomyślnie zespawniona
                if (spawnedEntityIds.Count > 0)
                {
                    isSpawnSuccessful = true;
                    // Oblicz rzeczywiste centrum siatki i aktualizuj sphereCenter
                    sphereCenter = CalculateGridCenter(spawnedEntityIds); // Zaktualizowane centrum sfery
                    LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"SphereCenter Coords: {sphereCenter.X}, {sphereCenter.Y}, {sphereCenter.X}");
                    this.ZoneRadius = Radius; // Przechowuje wartość w polu klasy
                                              // Aktualizacja zakończona, twórz strefę bezpieczeństwa
                    CreateSafeZoneAroundWarZone();
                    break; // Wyjdź z pętli, jeśli spawn się powiódł
                }
                else if (attempt < maxSpawnAttempts)
                {
                    LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Attempt {attempt} to spawn the grid failed. Trying again...");
                    await Task.Delay(5000); // Czeka 5 sekund przed kolejną próbą
                }
            }

            if (!isSpawnSuccessful)
            {
                LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "The grid could not be welded together after multiple attempts. The safety zone will not be created.");
                EventSystemMain.ChatManager.SendMessageAsOther(EventName, "Due to unexpected technical issues, the event cannot be initiated. We apologize for the inconvenience.", Color.Red);

            }
            else
            {
                // Subskrypcja sprawdzania pozycji graczy co sekundę
                SubscribeToUpdatePerSecond(CheckPlayersInSphere);

                // Inicjalizacja i konfiguracja timera
                messageAndGpsTimer = new System.Timers.Timer(settings.MessageAndGpsBroadcastIntervalSeconds * 1000);
                messageAndGpsTimer.Elapsed += async (sender, e) => await SendEventMessagesAndGps();
                messageAndGpsTimer.AutoReset = true;
                messageAndGpsTimer.Enabled = true;

                // Rozpocznij timer od razu
                await SendEventMessagesAndGps();

            }

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"System Start {EventName}.");
        }

        private async Task SendEventMessagesAndGps()
        {
            // Wysyłanie ogólnej wiadomości o rozpoczęciu eventu
            EventSystemMain.ChatManager.SendMessageAsOther(EventName, $"Start of the {EventName} event at coordinates: X={sphereCenter.X:F2}, Y={sphereCenter.Y:F2}, Z={sphereCenter.Z:F2}!", Color.Red);

            // Pobieranie listy wszystkich graczy online i wysyłanie do nich informacji GPS
            foreach (var player in MySession.Static.Players.GetOnlinePlayers()?.ToList() ?? new List<MyPlayer>())
            {
                long playerId = player.Identity.IdentityId;
                // Tutaj wywołujesz metodę SendGpsToPlayer dla każdego gracza online
                SendGpsToPlayer(playerId, $"{EventName} Event", sphereCenter, $"Location of the {EventName} event!", TimeSpan.FromSeconds(_config.WarZoneGridSettings.MessageAndGpsBroadcastIntervalSeconds), color: Color.Red);
            }
        }

        public override async Task SystemEndEvent()
        {
            // Rezygnacja z subskrypcji sprawdzania pozycji graczy
            UnsubscribeFromUpdatePerSecond(CheckPlayersInSphere);

            // Czyszczenie stanu graczy i ostatnich przyznanych punktów
            playersInSphere.Clear();
            playerMessageSent.Clear();
            lastPointsAwarded.Clear();
            ParticipatingPlayers.Clear();
            playerEntryTime.Clear();

            RemoveSafeZoneAroundWarZone();

            // Zatrzymaj i wyczyść timer
            if (messageAndGpsTimer != null)
            {
                messageAndGpsTimer.Stop();
                messageAndGpsTimer.Dispose();
            }

            await CleanupGrids();

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Ending {EventName}.");
            await Task.CompletedTask;
        }

        private void CheckPlayersInSphere()
        {
            var now = DateTime.UtcNow;
            var newEnemiesInZone = new HashSet<long>();
            bool stateChanged = false;

            foreach (var player in MySession.Static.Players.GetOnlinePlayers()?.ToList() ?? new List<MyPlayer>())
            {
                var character = player.Character;
                if (character != null)
                {
                    long playerId = player.Identity.IdentityId;
                    Vector3D playerPosition = character.PositionComp.GetPosition();
                    bool isInSphereNow = IsPlayerInZone(playerPosition, sphereCenter, ZoneRadius);
                    bool wasInSphereBefore = playersInSphere.ContainsKey(playerId) && playersInSphere[playerId];

                    // Sprawdzenie przynależności do frakcji przed dodaniem gracza do strefy
                    var playerFaction = MySession.Static.Factions.TryGetPlayerFaction(playerId);
                    if (playerFaction == null && isInSphereNow)
                    {
                        // Gracz bez frakcji w strefie, wysyłaj komunikat, ale nie dodawaj go do strefy
                        SendMessageByPlayerId(playerId, $"You must be part of a faction to participate in the {EventName} event!", Color.Red);
                        continue; // Pomiń dodawanie gracza do listy uczestników
                    }

                    if (isInSphereNow)
                    {
                        playersInSphere[playerId] = true;

                        if (!wasInSphereBefore)
                        {
                            // Gracz wszedł do strefy
                            playerEntryTime[playerId] = now;
                            SendMessageByPlayerId(playerId, "You entered the WarZone!", Color.Green);
                            stateChanged = true;
                        }

                        if (IsEnemy(playerId))
                        {
                            // Wykryto wroga w strefie
                            newEnemiesInZone.Add(playerId);
                        }
                    }
                    else if (wasInSphereBefore)
                    {
                        // Gracz opuścił strefę
                        playersInSphere.TryRemove(playerId, out _);
                        SendMessageByPlayerId(playerId, "You left the WarZone!", Color.Red);
                        stateChanged = true;
                    }
                }
            }

            if (stateChanged || !newEnemiesInZone.SetEquals(currentEnemiesInZone))
            {
                // Aktualizuj obecność wrogów tylko, jeśli doszło do zmiany stanu
                UpdateEnemyPresence(newEnemiesInZone);
            }

            AwardPointsToPlayers(now);
        }


        private void UpdateEnemyPresence(HashSet<long> newEnemiesInZone)
        {
            bool previouslyEnemyInZone = enemyInZone;
            enemyInZone = newEnemiesInZone.Any();

            // Sprawdź, czy doszło do zmiany z obecności wrogów na ich brak
            if (previouslyEnemyInZone && !enemyInZone)
            {
                // Wrogowie opuścili strefę, wyślij powiadomienie tylko raz
                SendMessageToPlayersInZone("Enemies have left the zone. You can now continue earning points!", Color.Green);
            }
            else if (!previouslyEnemyInZone && enemyInZone)
            {
                // Wrogowie pojawili się w strefie, zaktualizuj obecność
                SendMessageToPlayersInZone("An enemy has entered the zone, eliminate them to continue earning points!", Color.Red);
            }

            // Aktualizuj listę obecnych wrogów
            currentEnemiesInZone = newEnemiesInZone;
        }

        private void AwardPointsToPlayers(DateTime now)
        {
            if (!enemyInZone)
            {
                foreach (var kvp in playersInSphere)
                {
                    long playerId = kvp.Key;
                    ulong steamId = MySession.Static.Players.TryGetSteamId(playerId);
                    bool isPlayerCurrentlyInZone = kvp.Value;

                    // Sprawdź, czy gracz jest aktualnie w strefie.
                    if (isPlayerCurrentlyInZone && playerEntryTime.TryGetValue(playerId, out var entryTime))
                    {
                        double totalSecondsInZone = (now - entryTime).TotalSeconds;

                        // Sprawdź, czy upłynął wystarczający czas od momentu wejścia do strefy.
                        if (totalSecondsInZone >= _config.WarZoneGridSettings.PointsAwardIntervalSeconds)
                        {
                            // Resetuj czas wejścia gracza do bieżącego momentu, aby ponownie zacząć liczenie czasu spędzonego w strefie
                            playerEntryTime[playerId] = now;

                            // Przyznaj punkty graczowi
                            int pointsToAward = _config.WarZoneGridSettings.PointsPerInterval;
                            AwardPlayer((long)steamId, pointsToAward);
                            SendMessageByPlayerId(playerId, $"You have earned {pointsToAward} points for staying in the {EventName}!", Color.Yellow);
                        }
                    }
                    else if (!isPlayerCurrentlyInZone)
                    {
                        // Jeśli gracz opuścił strefę, usuń jego czas wejścia, aby nie liczyć czasu spędzonego poza strefą
                        DateTime removedTime;
                        playerEntryTime.TryRemove(playerId, out removedTime);
                    }
                }
            }
        }

        private void SendMessageToPlayersInZone(string message, Color color)
        {
            // Wysyłaj komunikat tylko do graczy, którzy są w strefie
            foreach (var playerId in playersInSphere.Keys)
            {
                if (playersInSphere.TryGetValue(playerId, out bool isInZone) && isInZone)
                {
                    SendMessageByPlayerId(playerId, message, color);
                }
            }
        }


        // Implementacja metody IsEnemy
        private bool IsEnemy(long playerId)
        {
            var playerFaction = MySession.Static.Factions.TryGetPlayerFaction(playerId);
            if (playerFaction == null)
            {
                // Gracz nie należy do żadnej frakcji, więc nie jest traktowany jako wróg
                return false;
            }

            // Sprawdzamy, czy jakiekolwiek frakcje obecne w strefie są wrogie wobec frakcji gracza
            foreach (var otherPlayerId in playersInSphere.Keys)
            {
                if (otherPlayerId == playerId)
                {
                    continue; // Pomijamy samego siebie
                }

                var otherPlayerFaction = MySession.Static.Factions.TryGetPlayerFaction(otherPlayerId);
                if (otherPlayerFaction != null)
                {
                    if (playerFaction.FactionId == otherPlayerFaction.FactionId)
                    {
                        continue; // To ten sam związek, nie wróg
                    }

                    // Sprawdź, czy frakcje są wrogie względem siebie
                    if (MySession.Static.Factions.AreFactionsEnemies(playerFaction.FactionId, otherPlayerFaction.FactionId))
                    {
                        return true; // Znaleziono wrogą frakcję
                    }
                }
            }

            // Jeśli dotarliśmy tutaj, oznacza to, że nie ma wrogich frakcji w strefie wobec frakcji gracza
            return false;
        }

        public override Task LoadEventSettings(EventSystemConfig config)
        {
            if (config.WarZoneGridSettings == null)
            {
                config.WarZoneGridSettings = new WarZoneGridConfig
                {
                    EventName = "WarZoneGrid",
                    IsEnabled = false,
                    ActiveDaysOfMonth = new List<int> { 1, 15, 20 },
                    StartTime = "00:00:00",
                    EndTime = "23:59:59",
                    OwnerGrid = 144115188075855881,
                    PrefabName = "PrefabName",
                    PointsAwardIntervalSeconds = 60,
                    MessageAndGpsBroadcastIntervalSeconds = 300,
                    PointsPerInterval = 10,
                    Radius = 100,
                    Shape = ZoneShape.Cube,
                    MinCoords = new AreaCoords(-1000, -1000, -1000),
                    MaxCoords = new AreaCoords(1000, 1000, 1000),
                    RandomizationType = CoordinateRandomizationType.Line,
                    EventDescription = $"The {EventName} event transforms an area within the game world into a volatile combat zone centered around a strategically significant grid structure. This grid becomes the focal point of intense skirmishes and battles, drawing players into a fight for control and dominance. Participants are thrust into the heart of conflict, where they must utilize their combat skills, strategic thinking, and teamwork to overcome opponents and secure the area. Rewards are allocated based on participation, success in combat, and the ability to hold and defend the grid against adversaries. This event challenges players to adapt quickly to changing combat scenarios, making alliances when necessary, and employing every tactic at their disposal to emerge victorious in the relentless battle for supremacy.",
                };
            }

            var settings = config.WarZoneGridSettings;
            IsEnabled = settings.IsEnabled;
            ActiveDaysOfMonth = settings.ActiveDaysOfMonth;
            StartTime = TimeSpan.Parse(settings.StartTime);
            EndTime = TimeSpan.Parse(settings.EndTime);

            string activeDaysText = ActiveDaysOfMonth.Count > 0 ? string.Join(", ", ActiveDaysOfMonth) : "Every day";
            LoggerHelper.DebugLog(Log, _config, $"Loaded {EventName} settings: IsEnabled={IsEnabled}, Active Days of Month={activeDaysText}, StartTime={StartTime}, EndTime={EndTime}");

            return Task.CompletedTask;
        }

        public bool IsPlayerInZone(Vector3D playerPosition, Vector3D sphereCenter, double ZoneRadius)
        {
            if (_config.WarZoneGridSettings.Shape == ZoneShape.Sphere)
            {
                // Logika dla sfery
                double distance = Vector3D.Distance(playerPosition, sphereCenter);
                return distance <= ZoneRadius;
            }
            else if (_config.WarZoneGridSettings.Shape == ZoneShape.Cube)
            {
                // Logika dla sześcianu
                double halfEdgeLength = ZoneRadius / 2;
                Vector3D minCoords = sphereCenter - new Vector3D(halfEdgeLength);
                Vector3D maxCoords = sphereCenter + new Vector3D(halfEdgeLength);

                return playerPosition.X >= minCoords.X && playerPosition.X <= maxCoords.X
                    && playerPosition.Y >= minCoords.Y && playerPosition.Y <= maxCoords.Y
                    && playerPosition.Z >= minCoords.Z && playerPosition.Z <= maxCoords.Z;
            }
            return false;
        }

        private Vector3D RandomizePosition(WarZoneGridConfig settings)
        {
            Random rnd = new Random();
            double x, y, z;

            // Konwersja AreaCoords na Vector3D
            Vector3D minCoords = new Vector3D(settings.MinCoords.X, settings.MinCoords.Y, settings.MinCoords.Z);
            Vector3D maxCoords = new Vector3D(settings.MaxCoords.X, settings.MaxCoords.Y, settings.MaxCoords.Z);

            switch (settings.RandomizationType)
            {
                case CoordinateRandomizationType.Line:
                    // Losowanie w linii prostej
                    double t = rnd.NextDouble(); // Parametr interpolacji
                    x = (1 - t) * minCoords.X + t * maxCoords.X;
                    y = (1 - t) * minCoords.Y + t * maxCoords.Y;
                    z = (1 - t) * minCoords.Z + t * maxCoords.Z;
                    break;
                case CoordinateRandomizationType.Sphere:
                    // Środek sfery to średnia wartość koordynatów Min i Max
                    Vector3D center = (minCoords + maxCoords) / 2;

                    // Promień sfery to połowa najmniejszego wymiaru sześcianu ograniczającego
                    double radius = Math.Min(Math.Min(
                        maxCoords.X - minCoords.X,
                        maxCoords.Y - minCoords.Y),
                        maxCoords.Z - minCoords.Z) / 2;

                    // Losowanie w obrębie sfery
                    Vector3D direction = Vector3D.Normalize(new Vector3D(rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5));
                    double distance = rnd.NextDouble() * radius;
                    x = center.X + direction.X * distance;
                    y = center.Y + direction.Y * distance;
                    z = center.Z + direction.Z * distance;
                    break;

                case CoordinateRandomizationType.Cube:
                    // Losowanie w obrębie sześcianu
                    x = rnd.NextDouble() * (maxCoords.X - minCoords.X) + minCoords.X;
                    y = rnd.NextDouble() * (maxCoords.Y - minCoords.Y) + minCoords.Y;
                    z = rnd.NextDouble() * (maxCoords.Z - minCoords.Z) + minCoords.Z;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return new Vector3D(x, y, z);
        }

        // Metoda do tworzenia SafeZone wokół wylosowanej pozycji
        private void CreateSafeZoneAroundWarZone()
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                try
                {
                    // Przygotowanie definicji SafeZone
                    var safezoneDefinition = new MyObjectBuilder_SafeZone
                    {
                        PositionAndOrientation = new MyPositionAndOrientation(sphereCenter, Vector3.Forward, Vector3.Up),
                        Enabled = true,
                        AccessTypePlayers = MySafeZoneAccess.Blacklist,
                        AccessTypeFactions = MySafeZoneAccess.Blacklist,
                        AccessTypeGrids = MySafeZoneAccess.Whitelist,
                        AccessTypeFloatingObjects = MySafeZoneAccess.Blacklist,
                        AllowedActions = MySafeZoneAction.All,
                        ModelColor = new SerializableVector3(1f, 0f, 0f),
                        Texture = "SafeZone_Texture_Restricted",
                        IsVisible = true,
                        DisplayName = $"{EventName}SafeZone"
                    };

                    // Ustawienie rozmiaru strefy
                    float zoneSize = (float)ZoneRadius;
                    if (_config.WarZoneGridSettings.Shape == ZoneShape.Sphere)
                    {
                        safezoneDefinition.Shape = MySafeZoneShape.Sphere;
                        safezoneDefinition.Radius = zoneSize;
                    }
                    else // Dla boxa
                    {
                        safezoneDefinition.Shape = MySafeZoneShape.Box;
                        safezoneDefinition.Size = new Vector3(zoneSize, zoneSize, zoneSize);
                    }

                    // Tworzenie encji z definicji
                    var safezoneEntity = MyAPIGateway.Entities.CreateFromObjectBuilder(safezoneDefinition);

                    if (safezoneEntity != null)
                    {
                        // Rejestrowanie encji w grze
                        MyAPIGateway.Entities.AddEntity(safezoneEntity, true);
                        safezoneEntityIds.TryAdd(safezoneEntity.EntityId, true);
                        //Log.Info($"Safezone created successfully with EntityId: {safezoneEntity.EntityId}");
                    }
                    else
                    {
                        Log.Error("Failed to create safezone. Entity creation returned null.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception occurred while creating safezone: {ex.Message}");
                    LoggerHelper.DebugLog(Log, _config, "Exception details: ", ex);
                }
            });
        }

        private void RemoveSafeZoneAroundWarZone()
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                var safezoneIds = safezoneEntityIds.Keys.ToList();

                foreach (var safezoneId in safezoneIds)
                {
                    if (MyAPIGateway.Entities.TryGetEntityById(safezoneId, out var safezoneEntity))
                    {
                        safezoneEntity.Close();
                        MyAPIGateway.Entities.RemoveEntity(safezoneEntity);

                        safezoneEntityIds.TryRemove(safezoneId, out _);
                    }
                }
            });
        }

        public class WarZoneGridConfig
        {
            public string EventName { get; set; }
            public bool IsEnabled { get; set; }
            public List<int> ActiveDaysOfMonth { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public string PrefabName { get; set; }
            public int PointsAwardIntervalSeconds { get; set; }
            public int MessageAndGpsBroadcastIntervalSeconds { get; set; }
            public int PointsPerInterval { get; set; }
            public double Radius { get; set; }
            public ZoneShape Shape { get; set; }
            public AreaCoords MinCoords { get; set; }
            public AreaCoords MaxCoords { get; set; }
            public CoordinateRandomizationType RandomizationType { get; set; }
            public long OwnerGrid {  get; set; }
            public string EventDescription { get; set; }
        }
    }
}
