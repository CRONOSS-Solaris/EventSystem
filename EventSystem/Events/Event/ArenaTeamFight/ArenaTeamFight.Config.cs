using EventSystem.Events;
using EventSystem.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace EventSystem.Event
{
    public partial class ArenaTeamFight : EventsBase
    {
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

            if (config.ArenaTeamFightSettings.WeaponLoadout == null || !config.ArenaTeamFightSettings.WeaponLoadout.Any())
            {
                config.ArenaTeamFightSettings.WeaponLoadout = new List<WeaponConfig>
                {
                    new WeaponConfig
                    {
                        WeaponSubtypeID = "SemiAutoPistolItem",
                        AmmoSubtypeID = "SemiAutoPistolMagazine",
                        AmmoQuantity = 60,
                        Chance = 0.20
                    },

                    new WeaponConfig
                    {
                        WeaponSubtypeID = "FullAutoPistolItem",
                        AmmoSubtypeID = "FullAutoPistolMagazine",
                        AmmoQuantity = 120,
                        Chance = 0.15
                    },

                    new WeaponConfig
                    {
                        WeaponSubtypeID = "ElitePistolItem",
                        AmmoSubtypeID = "ElitePistolMagazine",
                        AmmoQuantity = 60,
                        Chance = 0.10
                    },

                    new WeaponConfig
                    {
                        WeaponSubtypeID = "AutomaticRifleItem",
                        AmmoSubtypeID = "AutomaticRifleGun_Mag_20rd",
                        AmmoQuantity = 60,
                        Chance = 0.20
                    },

                    new WeaponConfig
                    {
                        WeaponSubtypeID = "PreciseAutomaticRifleItem",
                        AmmoSubtypeID = "PreciseAutomaticRifleGun_Mag_5rd",
                        AmmoQuantity = 60,
                        Chance = 0.20
                    },

                    new WeaponConfig
                    {
                        WeaponSubtypeID = "RapidFireAutomaticRifleItem",
                        AmmoSubtypeID = "RapidFireAutomaticRifleGun_Mag_50rd",
                        AmmoQuantity = 60,
                        Chance = 0.10
                    },

                    new WeaponConfig
                    {
                        WeaponSubtypeID = "UltimateAutomaticRifleItem",
                        AmmoSubtypeID = "UltimateAutomaticRifleGun_Mag_30rd",
                        AmmoQuantity = 60,
                        Chance = 0.10
                    },
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

            public List<WeaponConfig> WeaponLoadout { get; set; } = new List<WeaponConfig>();
        }

        public class WeaponConfig
        {
            public string WeaponSubtypeID { get; set; }
            public string AmmoSubtypeID { get; set; }
            public int AmmoQuantity { get; set; }
            public double Chance { get; set; }
        }
    }
}
