using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventSystem.Utils
{
    public class CommandInCooldown
    {
        private static readonly Lazy<CommandInCooldown> _lazyInstance = new Lazy<CommandInCooldown>(() => new CommandInCooldown());

        public static CommandInCooldown Instance => _lazyInstance.Value;

        private readonly ConcurrentDictionary<ulong, DateTime> _lastCommandUsage = new ConcurrentDictionary<ulong, DateTime>();

        // Prywatny konstruktor, aby zapobiec bezpośredniemu tworzeniu instancji klasy.
        private CommandInCooldown()
        {
        }

        public bool IsCommandInCooldown(ulong steamId, TimeSpan cooldown)
        {
            if (_lastCommandUsage.TryGetValue(steamId, out DateTime lastUsage))
            {
                return (DateTime.UtcNow - lastUsage) < cooldown;
            }
            return false;
        }

        public void UpdateCommandUsage(ulong steamId)
        {
            _lastCommandUsage[steamId] = DateTime.UtcNow;
        }

        public TimeSpan TimeLeftForCommand(ulong steamId, TimeSpan cooldown)
        {
            if (_lastCommandUsage.TryGetValue(steamId, out DateTime lastUsage))
            {
                var timeSinceLastUsage = DateTime.UtcNow - lastUsage;
                if (timeSinceLastUsage >= cooldown)
                {
                    return TimeSpan.Zero;
                }
                return cooldown - timeSinceLastUsage;
            }
            return TimeSpan.Zero;
        }
    }
}
