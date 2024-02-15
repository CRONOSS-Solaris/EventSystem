using EventSystem.Events;
using EventSystem.Managers;
using EventSystem.Utils;
using Sandbox.ModAPI;
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

                        LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Player {attacker} killed player {victim}. Team {attackerTeam.Name} scores {_config.ArenaTeamFightSettings.PointsPerKill} points.");

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
            var totalChance = _config.ArenaTeamFightSettings.WeaponLoadout.Sum(w => w.Chance);
            var roll = new Random().NextDouble() * totalChance;
            var cumulative = 0.0;
            foreach (var weaponConfig in _config.ArenaTeamFightSettings.WeaponLoadout)
            {
                cumulative += weaponConfig.Chance;
                if (roll <= cumulative)
                {
                    var weaponReward = new RewardItem
                    {
                        ItemTypeId = "MyObjectBuilder_PhysicalGunObject",
                        ItemSubtypeId = weaponConfig.WeaponSubtypeID,
                        Amount = 1
                    };

                    var ammoReward = new RewardItem
                    {
                        ItemTypeId = "MyObjectBuilder_AmmoMagazine",
                        ItemSubtypeId = weaponConfig.AmmoSubtypeID,
                        Amount = weaponConfig.AmmoQuantity
                    };

                    PlayerItemRewardManager.AwardPlayer((ulong)playerId, weaponReward, weaponReward.Amount, Log, _config);
                    PlayerItemRewardManager.AwardPlayer((ulong)playerId, ammoReward, ammoReward.Amount, Log, _config);
                    break;
                }
            }
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

        protected override async Task OnPlayerLeave(long steamId)
        {
            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Player with SteamID {steamId} is leaving the event.");

            // Usuń gracza z listy uczestników
            ParticipatingPlayers.TryRemove(steamId, out _);

            // Znajdź zespół, do którego należy gracz, i usuń go z tego zespołu
            Team playerTeam = null;
            foreach (var team in Teams.Values)
            {
                if (team.Members.TryRemove(steamId, out _))
                {
                    playerTeam = team;
                    break;
                }
            }
            if (playerTeam != null)
            {
                LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Player with SteamID {steamId} removed from team {playerTeam.Name}.");
            }

            ClearPlayerInventory(steamId);
            UnsubscribeFromCharacterDeath(steamId);
            ReturnItemsToPlayer(steamId);
            TeleportPlayerBack(steamId);

            LoggerHelper.DebugLog(Log, EventSystemMain.Instance.Config, $"Player with SteamID {steamId} has been processed for leaving.");
        }

    }
}
