using NLog;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using VRage.Plugins;
using static EventSystem.Events.EventsBase;

namespace EventSystem
{
    public partial class WarZoneConfigurationControl : UserControl
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystemMain/WarZoneConfigurationControl");
        private EventSystemMain Plugin { get; }
        private int clickCount = 0;
        private DispatcherTimer clickTimer = new DispatcherTimer();
        private DispatcherTimer unlockTimer = new DispatcherTimer();

        public WarZoneConfigurationControl()
        {
            InitializeComponent();

            // Timer dla podwójnego kliknięcia
            clickTimer.Interval = TimeSpan.FromMilliseconds(300);
            clickTimer.Tick += (s, e) => {
                clickTimer.Stop();
                clickCount = 0;
            };

            // Timer dla automatycznego zablokowania
            unlockTimer.Interval = TimeSpan.FromSeconds(30);
            unlockTimer.Tick += (s, e) => {
                overlay.Visibility = Visibility.Visible;
                actionsListBox.IsEnabled = false;
                unlockTimer.Stop();
            };
        }

        public WarZoneConfigurationControl(EventSystemMain plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;
            UpdateDaysTextBox();
            SetAllowedActions();
        }

        private void EnabledEventButton_Checked(object sender, RoutedEventArgs e)
        {
            if (Plugin.Config != null)
            {
                Plugin.Config.WarZoneSettings.IsEnabled = true;
                Plugin.Save();
            }
        }

        private void DisabledEventButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Plugin.Config != null)
            {
                Plugin.Config.WarZoneSettings.IsEnabled = false;
                Plugin.Save();
            }
        }

        // Metoda do aktualizacji TextBox na podstawie listy dni
        private void UpdateDaysTextBox()
        {
            DaysTextBox.Text = string.Join(", ", Plugin.Config.WarZoneSettings.ActiveDaysOfMonth);
        }

        // Metoda wywoływana po zmianie tekstu w TextBox (LostFocus lub podobne zdarzenie)
        private void DaysTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!(sender is TextBox textBox)) return;

            var newText = textBox.Text;
            if (newText.EndsWith(",") || newText.EndsWith(" "))
                return; // Jeśli użytkownik wpisuje kolejne liczby, nie reagujemy na każde naciśnięcie przecinka lub spacji.

            UpdateActiveDaysOfMonth();
        }

        private void DaysTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateActiveDaysOfMonth();
        }

        private void UpdateActiveDaysOfMonth()
        {
            var textBox = DaysTextBox;
            var daysText = textBox.Text;
            var daysList = daysText.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(day => int.TryParse(day, out int result) && result >= 1 && result <= 31 ? result : (int?)null)
                                   .Where(day => day != null)
                                   .Select(day => day.Value)
                                   .Distinct()
                                   .ToList();

            Plugin.Config.WarZoneSettings.ActiveDaysOfMonth = daysList;
        }

        private void SetAllowedActions()
        {
            if (actionsListBox == null)
            {
                Log.Error("actionsListBox is not initialized.");
                return;
            }
            if (Plugin.Config == null)
            {
                Log.Error("Plugin.Config is null.");
                return;
            }

            actionsListBox.SelectedItems.Clear();
            MySafeZoneAction configActions = Plugin.Config.WarZoneSettings.AllowedActions;
            Log.Info($"Configured actions: {configActions}");

            foreach (ListBoxItem item in actionsListBox.Items)
            {
                if (Enum.TryParse<MySafeZoneAction>(item.Content.ToString(), out MySafeZoneAction action))
                {
                    if ((configActions & action) == action)
                    {
                        item.IsSelected = true;
                        Log.Info($"Selecting action: {action}");
                    }
                    else
                    {
                        Log.Info($"Not selecting action: {action}");
                    }
                }
                else
                {
                    Log.Error($"Failed to parse '{item.Content}' as MySafeZoneAction.");
                }
            }
        }


        private void ActionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox != null && Plugin.Config != null)
            {
                MySafeZoneAction selectedActions = MySafeZoneAction.None; // Start with no actions selected

                foreach (ListBoxItem item in listBox.SelectedItems)
                {
                    if (Enum.TryParse<MySafeZoneAction>(item.Content.ToString(), out MySafeZoneAction action))
                    {
                        selectedActions |= action; // Combine actions using bitwise OR
                    }
                }

                Plugin.Config.WarZoneSettings.AllowedActions = selectedActions;
                Plugin.Save();
                Log.Info($"Updated allowed actions to: {selectedActions}");
            }
        }

        private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            clickCount++;
            if (clickCount == 1)
            {
                clickTimer.Start();
            }
            else if (clickCount == 2)
            {
                clickTimer.Stop();
                clickCount = 0;
                UnlockActions(sender, new RoutedEventArgs());
            }
        }


        private void UnlockActions(object sender, RoutedEventArgs e)
        {
            overlay.Visibility = Visibility.Collapsed;
            actionsListBox.IsEnabled = true;
            unlockTimer.Start();
        }

    }
}
