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
        private DateTime lastEnemyAlertSent = DateTime.MinValue;


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
            foreach (var player in MySession.Static.Players.GetOnlinePlayers())
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

            // Wiadomości do wysłania
            EventSystemMain.ChatManager.SendMessageAsOther($"WarZone", $"Start of the WarZone event!", Color.Red);
            EventSystemMain.ChatManager.SendMessageAsOther($"WarZone", $"GPS:WarZone:{sphereCenter.X}:{sphereCenter.Y}:{sphereCenter.Z}:#FFF17575:", Color.Red);

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "System Start WarZone.");
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

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, "Ending WarZone.");
            await Task.CompletedTask;
        }

        private void CheckPlayersInSphere()
        {
            var now = DateTime.UtcNow;
            var currentEnemiesInZone = new HashSet<long>();
            var playersInZoneWithEnemies = new HashSet<long>();

            // Sprawdzanie i zapisywanie obecnych wrogów w strefie
            foreach (var player in MySession.Static.Players.GetOnlinePlayers())
            {
                var character = player.Character;
                if (character != null)
                {
                    long playerId = player.Identity.IdentityId;
                    Vector3D playerPosition = character.PositionComp.GetPosition();
                    if (IsPlayerInSphere(playerPosition, sphereCenter, sphereRadius))
                    {
                        var playerFaction = MySession.Static.Factions.TryGetPlayerFaction(playerId);
                        if (playerFaction == null)
                        {
                            if (!playersInSphere.ContainsKey(playerId) || !playersInSphere[playerId])
                            {
                                EventSystemMain.ChatManager.SendMessageAsOther("WarZone", "You must be part of a faction to participate in the WarZone event!", Color.Red, player.Id.SteamId);
                            }
                            // Nie zapisujemy graczy bez frakcji jako obecnych w strefie
                            continue;
                        }

                        // Aktualizujemy stan obecności gracza w strefie
                        playersInSphere[playerId] = true;
                        if (IsEnemy(playerId))
                        {
                            currentEnemiesInZone.Add(playerId);
                            // Logowanie informacji o wykrytym wrogim graczu
                            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Enemy detected: {player.DisplayName} (SteamID: {player.Id.SteamId}) in the zone.");
                        }
                    }
                    else
                    {
                        // Gracz opuścił strefę
                        playersInSphere[playerId] = false;
                    }
                }
            }

            // Przesyłanie powiadomień do graczy w strefie, jeśli są wrogowie
            foreach (var playerId in playersInSphere.Keys)
            {
                if (playersInSphere[playerId])
                {
                    var playerFaction = MySession.Static.Factions.TryGetPlayerFaction(playerId);
                    if (playerFaction != null)
                    {
                        if (currentEnemiesInZone.Count > 0)
                        {
                            // Jeśli są wrogowie w strefie, zapisujemy, że gracz był w strefie z wrogami
                            playersInZoneWithEnemies.Add(playerId);

                            if ((now - lastEnemyAlertSent).TotalSeconds > 30)
                            {
                                EventSystemMain.ChatManager.SendMessageAsOther("WarZone", "An enemy has entered the zone, eliminate them to continue earning points!", Color.Red, MySession.Static.Players.TryGetSteamId(playerId));
                            }
                        }

                        // Obsługa przyznawania punktów, jeśli w strefie nie ma wrogów
                        if (!currentEnemiesInZone.Contains(playerId) && (!lastPointsAwarded.ContainsKey(playerId) || (now - lastPointsAwarded[playerId]).TotalSeconds >= _config.WarZoneSettings.PointsAwardIntervalSeconds))
                        {
                            AwardPlayer(playerId, _config.WarZoneSettings.PointsPerInterval).ConfigureAwait(false);
                            lastPointsAwarded[playerId] = now;
                            EventSystemMain.ChatManager.SendMessageAsOther("WarZone", $"You have earned {_config.WarZoneSettings.PointsPerInterval} points for staying in the WarZone!", Color.Yellow, MySession.Static.Players.TryGetSteamId(playerId));
                        }
                    }
                }
                else if (playersInZoneWithEnemies.Contains(playerId))
                {
                    // Gracz opuścił strefę, więc usuwamy go z listy graczy w strefie z wrogami
                    playersInZoneWithEnemies.Remove(playerId);
                }
            }

            // Aktualizacja czasu ostatniego wysłania alertu o wrogach
            if (currentEnemiesInZone.Count > 0 && (now - lastEnemyAlertSent).TotalSeconds > 30)
            {
                lastEnemyAlertSent = now;
            }
        }


        // Implementacja metody IsEnemy
        private bool IsEnemy(long playerId)
        {
            // Pobierz frakcję dla danego gracza
            var playerFaction = MySession.Static.Factions.TryGetPlayerFaction(playerId);

            if (playerFaction == null)
            {
                // Gracz nie należy do żadnej frakcji, więc nie jest traktowany jako wróg
                return false;
            }

            // Pobierz frakcje wszystkich graczy aktualnie w strefie
            foreach (var otherPlayerId in playersInSphere.Keys)
            {
                if (otherPlayerId == playerId)
                {
                    // Pomijamy samego siebie
                    continue;
                }

                var otherPlayerFaction = MySession.Static.Factions.TryGetPlayerFaction(otherPlayerId);
                if (otherPlayerFaction != null && playerFaction.FactionId != otherPlayerFaction.FactionId)
                {
                    // Sprawdź, czy frakcje są w stanie wojny
                    if (MySession.Static.Factions.AreFactionsEnemies(playerFaction.FactionId, otherPlayerFaction.FactionId))
                    {
                        // Jeśli frakcje są wrogie, uznajemy gracza za wroga
                        return true;
                    }
                }
            }

            return false;
        }

        public override Task CheckPlayerProgress(long steamId)
        {
            return Task.CompletedTask;
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
                    PointsPerInterval = 10,
                    SphereRadius = 100,
                    SphereMinCoords = new Vector3D(-1000, -1000, -1000),
                    SphereMaxCoords = new Vector3D(1000, 1000, 1000)
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
            public int PointsPerInterval { get; set; }
            public double SphereRadius { get; set; }
            public Vector3D SphereMinCoords { get; set; }
            public Vector3D SphereMaxCoords { get; set; }
        }

    }
}
