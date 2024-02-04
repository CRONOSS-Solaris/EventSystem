using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace EventSystem.Utils
{
    public static class Utilities
    {
        public static bool TryGetEntityByNameOrId(string nameOrId, out IMyEntity entity)
        {
            if (long.TryParse(nameOrId, out long id))
                return MyAPIGateway.Entities.TryGetEntityById(id, out entity);

            foreach (var ent in MyEntities.GetEntities())
            {
                if (ent.DisplayName == nameOrId)
                {
                    entity = ent;
                    return true;
                }
            }

            entity = null;
            return false;
        }

        public static bool TryGetPlayerBySteamId(long steamId, out IMyPlayer player)
        {
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                if ((long)p.SteamUserId == steamId)
                {
                    player = p;
                    return true;
                }
            }

            player = null;
            return false;
        }
    }
}
