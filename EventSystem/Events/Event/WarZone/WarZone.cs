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
    public class WarZone : EventsBase
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystem/WarZone");
        private readonly EventSystemConfig _config;
        private ConcurrentDictionary<long, bool> playersInSphere = new ConcurrentDictionary<long, bool>();
        private ConcurrentDictionary<long, bool> playerMessageSent = new ConcurrentDictionary<long, bool>();
        private ConcurrentDictionary<long, DateTime> lastPointsAwarded = new ConcurrentDictionary<long, DateTime>();
        private ConcurrentDictionary<long, DateTime> playerEntryTime = new ConcurrentDictionary<long, DateTime>();


        private HashSet<long> currentEnemiesInZone = new HashSet<long>();
        private System.Timers.Timer messageAndGpsTimer;


        private bool enemyInZone = false;


        private Vector3D sphereCenter;
        private double ZoneRadius;


        public WarZone(EventSystemConfig config)
        {
            _config = config;
            LoadEventSettings(config);
            EventName = _config.WarZoneSettings.EventName;
            AllowParticipationInOtherEvents = false;
            UseEventSpecificConfig = false;
            PrefabStoragePath = Path.Combine("EventSystem", "EventPrefabBlueprint");
        }

        public override string EventDescription => _config.WarZoneSettings.EventDescription;


        public override async Task SystemStartEvent()
        {
            // Dodanie wszystkich graczy do listy uczestników
            foreach (var player in MySession.Static.Players.GetOnlinePlayers()?.ToList() ?? new List<MyPlayer>())
            {
                long playerId = player.Identity.IdentityId;
                ParticipatingPlayers.TryAdd(playerId, true);
            }

            var settings = _config.WarZoneSettings;

            // Losowanie pozycji sfery
            Vector3D sphereCenter = RandomizePosition(settings);
            double Radius = settings.Radius;

            // Przechowuje wartości w polach klasy, aby móc ich użyć w CheckPlayersInSphere
            this.sphereCenter = sphereCenter;
            this.ZoneRadius = Radius;

            ZoneShape shape = _config.WarZoneSettings.Shape == EventsBase.ZoneShape.Sphere ? ZoneShape.Sphere : ZoneShape.Cube;
            CreateSafeZone(sphereCenter, Radius, shape, true, _config.WarZoneSettings.AccessTypePlayers, _config.WarZoneSettings.AccessTypeFactions, _config.WarZoneSettings.AccessTypeGrids, _config.WarZoneSettings.AccessTypeFloatingObjects, MySafeZoneAction.Damage | MySafeZoneAction.Shooting, _config.WarZoneSettings.SafeZoneColor, _config.WarZoneSettings.SafeZoneTexture, true, $"{EventName}SafeZone");


            // Subskrypcja sprawdzania pozycji graczy co sekundę
            SubscribeToUpdatePerSecond(CheckPlayersInSphere);

            // Inicjalizacja i konfiguracja timera
            messageAndGpsTimer = new System.Timers.Timer(settings.MessageAndGpsBroadcastIntervalSeconds * 1000);
            messageAndGpsTimer.Elapsed += async (sender, e) => await SendEventMessagesAndGps();
            messageAndGpsTimer.AutoReset = true;
            messageAndGpsTimer.Enabled = true;

            // Rozpocznij timer od razu
            await SendEventMessagesAndGps();

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
                SendGpsToPlayer(playerId, $"{EventName} Event", sphereCenter, $"Location of the {EventName} event!", TimeSpan.FromSeconds(_config.WarZoneSettings.MessageAndGpsBroadcastIntervalSeconds), color: Color.Red);
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

            RemoveSafeZone();

            // Zatrzymaj i wyczyść timer
            if (messageAndGpsTimer != null)
            {
                messageAndGpsTimer.Stop();
                messageAndGpsTimer.Dispose();
            }

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Ending {EventName}.");
            await Task.CompletedTask;
        }

        private void CheckPlayersInSphere()
        {
            var now = DateTime.UtcNow;
            var newEnemiesInZone = new HashSet<long>();
            var currentlyOnlinePlayers = new HashSet<long>(MySession.Static.Players.GetOnlinePlayers()?.Select(p => p.Identity.IdentityId) ?? new HashSet<long>());
            bool stateChanged = false;

            // Sprawdzamy obecnych graczy w strefie
            foreach (var kvp in playersInSphere.ToList())
            {
                if (!currentlyOnlinePlayers.Contains(kvp.Key))
                {
                    // Gracz był w strefie, ale nie jest już online
                    playersInSphere.TryRemove(kvp.Key, out _);
                    SendMessageByPlayerId(kvp.Key, "You have been removed from the WarZone due to disconnect!", Color.Orange);
                    stateChanged = true;
                }
            }

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
                        if (totalSecondsInZone >= _config.WarZoneSettings.PointsAwardIntervalSeconds)
                        {
                            // Resetuj czas wejścia gracza do bieżącego momentu, aby ponownie zacząć liczenie czasu spędzonego w strefie
                            playerEntryTime[playerId] = now;

                            // Przyznaj punkty graczowi
                            int pointsToAward = _config.WarZoneSettings.PointsPerInterval;
                            AwardPlayer((long)steamId, pointsToAward);
                            SendMessageByPlayerId(playerId, $"You have earned {pointsToAward} points for staying in the WarZone!", Color.Yellow);
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
            if (config.WarZoneSettings == null)
            {
                config.WarZoneSettings = new WarZoneConfig
                {
                    EventName = "WarZone",
                    IsEnabled = false,
                    ActiveDaysOfMonth = new List<int> { 1, 15, 20 },
                    StartTime = "00:00:00",
                    EndTime = "23:59:59",
                    PointsAwardIntervalSeconds = 60,
                    MessageAndGpsBroadcastIntervalSeconds = 300,
                    PointsPerInterval = 10,
                    Radius = 100,
                    SafeZoneTexture = "SafeZone_Texture_Restricted",
                    SafeZoneColor = new SerializableVector3(1f, 0f, 0f),
                    AccessTypePlayers = MySafeZoneAccess.Blacklist,
                    AccessTypeFactions = MySafeZoneAccess.Blacklist,
                    AccessTypeGrids = MySafeZoneAccess.Blacklist,
                    AccessTypeFloatingObjects = MySafeZoneAccess.Blacklist,
                    AllowedActions = MySafeZoneAction.Damage | MySafeZoneAction.Shooting,
                    Shape = ZoneShape.Cube,
                    RandomizationType = CoordinateRandomizationType.Line,
                    MinCoords = new AreaCoords(-1000, -1000, -1000),
                    MaxCoords = new AreaCoords(1000, 1000, 1000),
                    EventDescription = $"The {EventName} event transforms a specific area into a contested war zone, where players and factions can compete for control and rewards. Upon entering the zone, participants are challenged to maintain their presence within a dynamically defined sphere, battling against each other and facing periodic challenges. Points are awarded based on survival time, combat achievements, and the ability to fend off enemies. This event emphasizes strategic teamwork, individual skill, and adaptability to changing conditions. The WarZone dynamically generates GPS coordinates to guide participants into the heart of the conflict, with rewards scaling based on performance and contribution to the event's objectives.",
                };
            }

            var settings = config.WarZoneSettings;
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
            if (_config.WarZoneSettings.Shape == ZoneShape.Sphere)
            {
                // Logika dla sfery
                double distance = Vector3D.Distance(playerPosition, sphereCenter);
                return distance <= ZoneRadius;
            }
            else if (_config.WarZoneSettings.Shape == ZoneShape.Cube)
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

        private Vector3D RandomizePosition(WarZoneConfig settings)
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

        public class WarZoneConfig
        {
            public string EventName { get; set; }
            public bool IsEnabled { get; set; }
            public List<int> ActiveDaysOfMonth { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public int PointsAwardIntervalSeconds { get; set; }
            public int MessageAndGpsBroadcastIntervalSeconds { get; set; }
            public int PointsPerInterval { get; set; }


            public ZoneShape Shape { get; set; }
            public double Radius { get; set; }
            public string SafeZoneTexture { get; set; }
            public SerializableVector3 SafeZoneColor { get; set; }

            public MySafeZoneAccess AccessTypePlayers { get; set; }
            public MySafeZoneAccess AccessTypeFactions { get; set; }
            public MySafeZoneAccess AccessTypeGrids { get; set; }
            public MySafeZoneAccess AccessTypeFloatingObjects { get; set; }
            public MySafeZoneAction AllowedActions { get; set; }

            public CoordinateRandomizationType RandomizationType { get; set; }
            public AreaCoords MinCoords { get; set; }
            public AreaCoords MaxCoords { get; set; }
            public string EventDescription { get; set; }
        }
    }
}
