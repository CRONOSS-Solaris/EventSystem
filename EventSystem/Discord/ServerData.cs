using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Library.Collections;

namespace EventSystem.Discord
{
    public sealed class ServerData
    {
        public IReadOnlyDictionary<ulong, DiscordRole> roles { get; set; }
        public DiscordGuild guild { get; set; }
        public MyList<DiscordMember> DiscordMembers { get; set; } = new MyList<DiscordMember>();
        public MyList<DiscordRole> DiscordRoles { get; set; } = new MyList<DiscordRole>();
    }
}
