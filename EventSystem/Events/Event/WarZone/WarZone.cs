using EventSystem.Events;
using EventSystem.Utils;
using NLog;
using Sandbox.Game.World;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        private ConcurrentDictionary<long, DateTime> playerExitTime = new ConcurrentDictionary<long, DateTime>();
        private HashSet<long> currentEnemiesInZone = new HashSet<long>();
        private System.Timers.Timer messageAndGpsTimer;


        private bool enemyInZone = false;


        private Vector3D sphereCenter;
        private double sphereRadius;


        public WarZone(EventSystemConfig config)
        {
            _config = config;
            EventName = "WarZone";
            AllowParticipationInOtherEvents = false;
            UseEventSpecificConfig = false;
            PrefabStoragePath = Path.Combine("EventSystem", "EventPrefab/Blueprint");
        }

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
            Vector3D sphereCenter = RandomizeSpherePosition(settings);
            double sphereRadius = settings.SphereRadius;

            // Przechowuje wartości w polach klasy, aby móc ich użyć w CheckPlayersInSphere
            this.sphereCenter = sphereCenter;
            this.sphereRadius = sphereRadius;

            // Subskrypcja sprawdzania pozycji graczy co sekundę
            SubscribeToUpdatePerSecond(CheckPlayersInSphere);

            // Inicjalizacja i konfiguracja timera
            messageAndGpsTimer = new System.Timers.Timer(settings.MessageAndGpsBroadcastIntervalSeconds * 1000);
            messageAndGpsTimer.Elapsed += async (sender, e) => await SendEventMessagesAndGps();
            messageAndGpsTimer.AutoReset = true;
            messageAndGpsTimer.Enabled = true;

            // Rozpocznij timer od razu
            await SendEventMessagesAndGps();

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "System Start WarZone.");
        }

        private async Task SendEventMessagesAndGps()
        {
            // Wysyłanie ogólnej wiadomości o rozpoczęciu eventu
            EventSystemMain.ChatManager.SendMessageAsOther("WarZone", $"Start of the WarZone event at coordinates: X={sphereCenter.X:F2}, Y={sphereCenter.Y:F2}, Z={sphereCenter.Z:F2}!", Color.Red);

            // Pobieranie listy wszystkich graczy online i wysyłanie do nich informacji GPS
            foreach (var player in MySession.Static.Players.GetOnlinePlayers()?.ToList() ?? new List<MyPlayer>())
            {
                long playerId = player.Identity.IdentityId;
                // Tutaj wywołujesz metodę SendGpsToPlayer dla każdego gracza online
                SendGpsToPlayer(playerId, "WarZone", sphereCenter, "Location of the WarZone event!", color: Color.Red);
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
            playerExitTime.Clear();

            // Zatrzymaj i wyczyść timer
            if (messageAndGpsTimer != null)
            {
                messageAndGpsTimer.Stop();
                messageAndGpsTimer.Dispose();
            }

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Ending WarZone.");
            await Task.CompletedTask;
        }

        private void CheckPlayersInSphere()
        {
            var now = DateTime.UtcNow;
            var newEnemiesInZone = new HashSet<long>();

            // Przygotowanie do sprawdzenia, czy doszło do zmiany w obecności wrogów.
            foreach (var player in MySession.Static.Players.GetOnlinePlayers()?.ToList() ?? new List<MyPlayer>())
            {
                var character = player.Character;
                if (character != null)
                {
                    long playerId = player.Identity.IdentityId;
                    Vector3D playerPosition = character.PositionComp.GetPosition();
                    bool isInSphereNow = IsPlayerInSphere(playerPosition, sphereCenter, sphereRadius);
                    bool wasInSphereBefore = playersInSphere.GetOrAdd(playerId, false);

                    // Zaktualizuj obecność gracza w strefie.
                    playersInSphere[playerId] = isInSphereNow;

                    if (isInSphereNow && !wasInSphereBefore)
                    {
                        playerEntryTime[playerId] = now;
                        // Powiadomienie o wejściu do strefy.
                        SendMessage(playerId, "You entered the WarZone!", Color.Green);
                    }
                    else if (!isInSphereNow && wasInSphereBefore)
                    {
                        playerExitTime[playerId] = now;
                        // Powiadomienie o opuszczeniu strefy.
                        SendMessage(playerId, "You left the WarZone!", Color.Red);
                    }

                    var playerFaction = MySession.Static.Factions.TryGetPlayerFaction(playerId);
                    if (playerFaction == null && isInSphereNow)
                    {
                        // Gracz w strefie bez frakcji.
                        SendMessage(playerId, "You must be part of a faction to participate in the WarZone event!", Color.Red);
                    }
                    else if (isInSphereNow && IsEnemy(playerId))
                    {
                        newEnemiesInZone.Add(playerId);
                    }
                }
            }

            // Aktualizacja stanu wrogów w strefie.
            UpdateEnemyPresence(newEnemiesInZone);

            // Przyznawanie punktów.
            AwardPointsToPlayers(now);
        }

        private void UpdateEnemyPresence(HashSet<long> newEnemiesInZone)
        {
            bool enemyPresenceChanged = !newEnemiesInZone.SetEquals(currentEnemiesInZone);
            if (enemyPresenceChanged)
            {
                enemyInZone = newEnemiesInZone.Any();
                currentEnemiesInZone = newEnemiesInZone;

                String message = enemyInZone ? "An enemy has entered the zone, eliminate them to continue earning points!" : "Enemies have left the zone. You can now continue earning points!";
                Color messageColor = enemyInZone ? Color.Red : Color.Green;
                SendMessageToAllPlayers(message, messageColor);
            }
        }

        private void AwardPointsToPlayers(DateTime now)
        {
            if (!enemyInZone)
            {
                foreach (var kvp in playersInSphere)
                {
                    long playerId = kvp.Key;
                    ulong steamId = MySession.Static.Players.TryGetSteamId(playerId);
                    bool isPlayerInZone = kvp.Value;

                    // Sprawdzamy, czy dla gracza został zarejestrowany czas wejścia do strefy.
                    if (playerEntryTime.TryGetValue(playerId, out var entryTime))
                    {
                        DateTime lastExitTime;
                        double totalSecondsInZone;

                        // Jeśli gracz obecnie znajduje się w strefie, używamy bieżącego czasu do obliczenia całkowitego czasu spędzonego w strefie.
                        // W przeciwnym przypadku używamy czasu ostatniego wyjścia do obliczeń, aby nie naliczać czasu poza strefą.
                        if (isPlayerInZone || !playerExitTime.TryGetValue(playerId, out lastExitTime))
                        {
                            totalSecondsInZone = (now - entryTime).TotalSeconds;
                        }
                        else
                        {
                            // Jeśli gracz opuścił strefę, obliczamy czas spędzony na podstawie ostatniego znanego czasu wyjścia.
                            totalSecondsInZone = (lastExitTime - entryTime).TotalSeconds;
                        }

                        var pointsAwardIntervalSeconds = _config.WarZoneSettings.PointsAwardIntervalSeconds;

                        // Obliczamy, ile pełnych interwałów czasowych upłynęło od czasu wejścia gracza do strefy.
                        int intervalsSpentInZone = (int)(totalSecondsInZone / pointsAwardIntervalSeconds);

                        if (intervalsSpentInZone > 0)
                        {
                            // Przyznajemy punkty za każdy pełny interwał spędzony w strefie.
                            int pointsToAward = intervalsSpentInZone * _config.WarZoneSettings.PointsPerInterval;

                            // Aktualizujemy czas wejścia gracza, przesuwając go o liczbę pełnych interwałów, które zostały już uwzględnione przy przyznawaniu punktów.
                            playerEntryTime[playerId] = entryTime.AddSeconds(intervalsSpentInZone * pointsAwardIntervalSeconds);

                            // Przyznajemy punkty graczowi.
                            AwardPlayer((long)steamId, pointsToAward);
                            SendMessage(playerId, $"You have earned {pointsToAward} points for staying in the WarZone!", Color.Yellow);
                        }

                        // Jeśli gracz opuścił strefę, zachowujemy jego czas wyjścia do późniejszego użycia.
                        if (!isPlayerInZone)
                        {
                            playerExitTime[playerId] = now;
                        }
                    }
                }
            }
        }


        private void SendMessage(long playerId, string message, Color color)
        {
            ulong steamId = MySession.Static.Players.TryGetSteamId(playerId);
            EventSystemMain.ChatManager.SendMessageAsOther("WarZone", message, color, steamId);
        }

        private void SendMessageToAllPlayers(string message, Color color)
        {
            foreach (var playerId in playersInSphere.Keys)
            {
                SendMessage(playerId, message, color);
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
                    IsEnabled = false,
                    ActiveDaysOfMonth = new List<int> { 1, 15, 20 },
                    StartTime = "00:00:00",
                    EndTime = "23:59:59",
                    PointsAwardIntervalSeconds = 60,
                    MessageAndGpsBroadcastIntervalSeconds = 300,
                    PointsPerInterval = 10,
                    SphereRadius = 100,
                    SphereMinCoords = new SphereCoords(-1000, -1000, -1000),
                    SphereMaxCoords = new SphereCoords(1000, 1000, 1000)
                };
            }

            var settings = config.WarZoneSettings;
            IsEnabled = settings.IsEnabled;
            ActiveDaysOfMonth = settings.ActiveDaysOfMonth;
            StartTime = TimeSpan.Parse(settings.StartTime);
            EndTime = TimeSpan.Parse(settings.EndTime);

            string activeDaysText = ActiveDaysOfMonth.Count > 0 ? string.Join(", ", ActiveDaysOfMonth) : "Every day";
            LoggerHelper.DebugLog(Log, _config, $"Loaded WarZone settings: IsEnabled={IsEnabled}, Active Days of Month={activeDaysText}, StartTime={StartTime}, EndTime={EndTime}");

            return Task.CompletedTask;
        }

        public bool IsPlayerInSphere(Vector3D playerPosition, Vector3D sphereCenter, double sphereRadius)
        {
            // Oblicz dystans między pozycją gracza a środkiem sfery
            double distance = Vector3D.Distance(playerPosition, sphereCenter);

            // Sprawdź, czy dystans jest mniejszy niż promień sfery
            return distance <= sphereRadius;
        }

        private Vector3D RandomizeSpherePosition(WarZoneConfig settings)
        {
            Random rnd = new Random();
            double x = rnd.NextDouble() * (settings.SphereMaxCoords.X - settings.SphereMinCoords.X) + settings.SphereMinCoords.X;
            double y = rnd.NextDouble() * (settings.SphereMaxCoords.Y - settings.SphereMinCoords.Y) + settings.SphereMinCoords.Y;
            double z = rnd.NextDouble() * (settings.SphereMaxCoords.Z - settings.SphereMinCoords.Z) + settings.SphereMinCoords.Z;

            return new Vector3D(x, y, z);
        }

        public class WarZoneConfig
        {
            public bool IsEnabled { get; set; }
            public List<int> ActiveDaysOfMonth { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public int PointsAwardIntervalSeconds { get; set; }
            public int MessageAndGpsBroadcastIntervalSeconds { get; set; }
            public int PointsPerInterval { get; set; }
            public double SphereRadius { get; set; }
            public SphereCoords SphereMinCoords { get; set; }
            public SphereCoords SphereMaxCoords { get; set; }
        }

        public class SphereCoords
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }

            public SphereCoords() { }

            // Konstruktor przyjmujący wartości X, Y, Z
            public SphereCoords(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            //// Metoda pomocnicza do konwersji na Vector3D
            //public Vector3D ToVector3D()
            //{
            //    return new Vector3D(X, Y, Z);
            //}
        }


    }
}
