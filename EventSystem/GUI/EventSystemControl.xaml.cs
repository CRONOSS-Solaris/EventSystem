using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace EventSystem
{
    public partial class EventSystemControl : UserControl
    {

        private EventSystemMain Plugin { get; }

        private EventSystemControl()
        {
            InitializeComponent();
        }

        public EventSystemControl(EventSystemMain plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;
        }

        private void SupportButton_OnClick(object sender, RoutedEventArgs e)
        {
            string discordInviteLink = "https://discord.gg/BUnUnXz5xJ";
            Process.Start(new ProcessStartInfo
            {
                FileName = discordInviteLink,
                UseShellExecute = true
            });
        }

        private void WikiButton_OnClick(object sender, RoutedEventArgs e)
        {
            string WikiLink = "https://wiki.torchapi.com/en/Plugins/EventSystem/EventSystem";
            Process.Start(new ProcessStartInfo
            {
                FileName = WikiLink,
                UseShellExecute = true
            });
        }

        private void EventSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (EventSelector.SelectedIndex)
            {
                case 0:
                    EventConfigurationContent.Content = new WarZoneConfigurationControl(Plugin);
                    break;
                case 1:
                    EventConfigurationContent.Content = new WarZoneGridConfigurationControl(Plugin);
                    break;
                case 2:
                    //EventConfigurationContent.Content = new ArenaTeamFightConfigurationControl(Plugin);
                    break;
            }
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Save();
        }
    }
}
