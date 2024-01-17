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

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Save();
        }
    }
}
