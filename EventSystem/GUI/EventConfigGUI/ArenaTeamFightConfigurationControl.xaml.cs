using NLog;
using Sandbox.Engine.Utils;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using VRage.Plugins;

namespace EventSystem
{
    public partial class ArenaTeamFightConfigurationControl : UserControl
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystemMain/ArenaTeamFightConfigurationControl");
        private EventSystemMain Plugin { get; }

        public ArenaTeamFightConfigurationControl()
        {
            InitializeComponent();
        }

        public ArenaTeamFightConfigurationControl(EventSystemMain plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;
        }

    }
}
