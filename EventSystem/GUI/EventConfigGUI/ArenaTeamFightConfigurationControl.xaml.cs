//using NLog;
//using Sandbox.Engine.Utils;
//using System;
//using System.Diagnostics;
//using System.Linq;
//using System.Windows;
//using System.Windows.Controls;
//using VRage.Plugins;

//namespace EventSystem
//{
//    public partial class ArenaTeamFightConfigurationControl : UserControl
//    {
//        public static readonly Logger Log = LogManager.GetLogger("EventSystemMain/ArenaTeamFightConfigurationControl");
//        private EventSystemMain Plugin { get; }

//        public ArenaTeamFightConfigurationControl()
//        {
//            InitializeComponent();
//        }

//        public ArenaTeamFightConfigurationControl(EventSystemMain plugin) : this()
//        {
//            Plugin = plugin;
//            DataContext = plugin.Config;
//        }

//        private void TimeTextBox_LostFocus(object sender, RoutedEventArgs e)
//        {
//            if (sender is TextBox timeTextBox)
//            {
//                if (!TimeSpan.TryParse(timeTextBox.Text, out TimeSpan timeResult))
//                {
//                    MessageBox.Show("Please enter a valid time in HH:mm:ss format.", "Invalid Time Format", MessageBoxButton.OK, MessageBoxImage.Error);
//                }
//            }
//        }

//        private void AddDay_Click(object sender, RoutedEventArgs e)
//        {
//            if (int.TryParse(NewDayTextBox.Text, out int newDay))
//            {
//                if (newDay >= 1 && newDay <= 31)
//                {
//                    if (!Plugin.Config.ArenaTeamFightSettings.ActiveDaysOfMonth.Contains(newDay))
//                    {
//                        Plugin.Config.ArenaTeamFightSettings.ActiveDaysOfMonth.Add(newDay);
//                        // Aktualizacja ItemsControl
//                        UpdateDaysOfMonthItemsControl();
//                        NewDayTextBox.Clear();
//                    }
//                    else
//                    {
//                        MessageBox.Show("The number given is already on the list.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                    }
//                }
//                else
//                {
//                    MessageBox.Show("The number specified must be between 1 and 31.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                }
//            }
//            else
//            {
//                MessageBox.Show("The value given is not an integer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//            }
//        }

//        private void RemoveSelectedDay_Click(object sender, RoutedEventArgs e)
//        {
//            if (int.TryParse(NewDayTextBox.Text, out int dayToRemove))
//            {
//                if (dayToRemove >= 1 && dayToRemove <= 31)
//                {
//                    if (Plugin.Config.ArenaTeamFightSettings.ActiveDaysOfMonth.Contains(dayToRemove))
//                    {
//                        Plugin.Config.ArenaTeamFightSettings.ActiveDaysOfMonth.Remove(dayToRemove);
//                        // Aktualizacja ItemsControl
//                        UpdateDaysOfMonthItemsControl();
//                        NewDayTextBox.Clear();
//                    }
//                    else
//                    {
//                        MessageBox.Show("The specified number does not exist in the list.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                    }
//                }
//                else
//                {
//                    MessageBox.Show("The number specified must be between 1 and 31.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                }
//            }
//            else
//            {
//                MessageBox.Show("The value given is not an integer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//            }
//        }



//        private void UpdateDaysOfMonthItemsControl()
//        {
//            var currentItemsSource = DaysItemsControl.ItemsSource;
//            DaysItemsControl.ItemsSource = null;
//            DaysItemsControl.ItemsSource = currentItemsSource;
//        }

//    }
//}
