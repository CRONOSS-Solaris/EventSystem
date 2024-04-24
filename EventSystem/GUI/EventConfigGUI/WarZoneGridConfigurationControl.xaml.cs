using NLog;
using Sandbox.Engine.Utils;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VRage.Plugins;

namespace EventSystem
{
    public partial class WarZoneGridConfigurationControl : UserControl
    {
        public static readonly Logger Log = LogManager.GetLogger("EventSystemMain/WarZoneGridConfigurationControl");
        private EventSystemMain Plugin { get; }

        public WarZoneGridConfigurationControl()
        {
            InitializeComponent();
        }

        public WarZoneGridConfigurationControl(EventSystemMain plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;
            UpdateDaysTextBox();
        }

        // Metoda do aktualizacji TextBox na podstawie listy dni
        private void UpdateDaysTextBox()
        {
            DaysTextBox.Text = string.Join(", ", Plugin.Config.WarZoneGridSettings.ActiveDaysOfMonth);
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

            Plugin.Config.WarZoneGridSettings.ActiveDaysOfMonth = daysList;
        }

    }
}
