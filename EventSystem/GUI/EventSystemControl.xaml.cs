using EventSystem.Config;
using EventSystem.Utils;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            DataContext = new DataContextProxy
            {
                Config = plugin.Config,
                Plugin = plugin
            };
            Loaded += OnLoaded;

            Plugin.OnItemRewardsConfigUpdated += Plugin_OnItemRewardsConfigUpdated;
            Plugin.OnPackRewardsConfigUpdated += Plugin_OnPackRewardsConfigUpdated;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Ustawiamy ItemsSource dla DataGrid po załadowaniu kontrolek
            IndividualRewardItemDataGrid.ItemsSource = Plugin.ItemRewardsConfig.IndividualItems;
            RewardSetsDataGrid.ItemsSource = Plugin.PackRewardsConfig.RewardSets;
        }

        private void Plugin_OnItemRewardsConfigUpdated(ItemRewardsConfig newConfig)
        {
            // Używamy Dispatchera, aby wykonać aktualizację na głównym wątku UI
            Dispatcher.Invoke(() =>
            {
                IndividualRewardItemDataGrid.ItemsSource = newConfig.IndividualItems;
                IndividualRewardItemDataGrid.Items.Refresh();
            });
        }

        private void Plugin_OnPackRewardsConfigUpdated(PackRewardsConfig newConfig)
        {
            Dispatcher.Invoke(() =>
            {
                // Aktualizacja ItemsSource dla RewardSetsDataGrid z nową konfiguracją
                RewardSetsDataGrid.ItemsSource = newConfig.RewardSets;
                RewardSetsDataGrid.Items.Refresh();
            });
        }


        public class DataContextProxy
        {
            public EventSystemConfig Config { get; set; }
            public EventSystemMain Plugin { get; set; }
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

        public void UpdateButtonState(bool isEnabled)
        {
            IndividualRewardItem.IsEnabled = isEnabled;
            PackRewardsItem.IsEnabled = isEnabled;
        }

        private void SaveButtonGS_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Save();
            Plugin.UpdateEventSystemConfig(Plugin.Config);
        }

        private void SaveButtonIRI_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Save();
            Plugin.UpdateItemRewardsConfig(Plugin.ItemRewardsConfig);
        }

        private void SaveButtonPRI_OnClick(object sender, RoutedEventArgs e)
        {
            // Sprawdzanie, czy wszystkie zestawy nagród mają unikalne nazwy
            var allRewardSetNames = Plugin.PackRewardsConfig.RewardSets.Select(rs => rs.Name).ToList();
            var uniqueRewardSetNames = allRewardSetNames.Distinct().Count();

            if (allRewardSetNames.Count != uniqueRewardSetNames)
            {
                MessageBox.Show("Each reward set must have a unique name.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return; // Zatrzymaj proces zapisu, jeśli nazwy nie są unikalne
            }

            // Kontynuuj proces zapisu, jeśli wszystkie nazwy są unikalne
            Plugin.Save();
            Plugin.UpdateConfigPackRewards(Plugin.PackRewardsConfig);
        }


        private void AddIndividualRewardItemButton_OnClick(object sender, RoutedEventArgs e)
        {
            // Tworzymy nowy obiekt RewardItem i dodajemy go do listy
            var newItem = new IndividualRewardItem();
            Plugin.ItemRewardsConfig.IndividualItems.Add(newItem);

            // Odświeżamy DataGrid
            IndividualRewardItemDataGrid.Items.Refresh();
        }

        private void DeleteIndividualRewardItemButton_OnClick(object sender, RoutedEventArgs e)
        {
            // Pobieramy aktualnie wybrany przedmiot
            var selectedItem = IndividualRewardItemDataGrid.SelectedItem as IndividualRewardItem;

            // Jeśli nic nie jest zaznaczone, nic nie robimy
            if (selectedItem == null)
                return;

            // Usuwamy zaznaczony przedmiot z listy
            Plugin.ItemRewardsConfig.IndividualItems.Remove(selectedItem);

            // Odświeżamy DataGrid
            IndividualRewardItemDataGrid.Items.Refresh();
        }

        private void ItemTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null)
            {
                Debug.WriteLine("ComboBox is null");
                return;
            }

            var selectedType = comboBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selectedType))
            {
                Debug.WriteLine("SelectedType is null or whitespace");
                return;
            }

            // Używamy interfejsu IRewardItem zamiast konkretnych klas
            var item = comboBox.DataContext as IRewardItem;
            if (item == null)
            {
                Debug.WriteLine("Item is null");
                return;
            }
           
            // Update the available subtypes for the selected type
            if (!Plugin.AvailableItemSubtypes.TryGetValue(selectedType, out var availableSubtypes))
            {
                Debug.WriteLine($"No available subtypes found for {selectedType}");
                availableSubtypes = new List<string>();
            }

            // Musimy rzutować item na konkretne klasy, aby zaktualizować AvailableSubTypeIds
            if (item is PackRewardItem packItem)
            {
                packItem.AvailableSubTypeIds = availableSubtypes;
            }
            else if (item is IndividualRewardItem individualItem)
            {
                individualItem.AvailableSubTypeIds = availableSubtypes;
            }

            // Find the other combobox and update its items source
            var parent = comboBox.Parent as FrameworkElement;
            while (!(parent is DataGridRow))
            {
                if (parent == null)
                {
                    Debug.WriteLine("Parent is null");
                    return;
                }
                parent = parent.Parent as FrameworkElement;
            }
            var row = parent as DataGridRow;
            var subtypeComboBox = FindVisualChild<ComboBox>(row, "ItemSubtypeComboBox");
            if (subtypeComboBox != null)
            {
                subtypeComboBox.ItemsSource = availableSubtypes;
                subtypeComboBox.SelectedItem = availableSubtypes.FirstOrDefault();
            }
            else
            {
                Debug.WriteLine("SubtypeComboBox is null");
            }
        }

        private T FindVisualChild<T>(DependencyObject obj, string name) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T typedChild && (string.IsNullOrEmpty(name) || (child is FrameworkElement fe && fe.Name == name)))
                {
                    return typedChild;
                }

                var foundChild = FindVisualChild<T>(child, name);
                if (foundChild != null)
                {
                    return foundChild;
                }
            }

            return null;
        }

        private void RewardSetsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is RewardSet selectedRewardSet)
            {
                // Znajdowanie zaktualizowanego zestawu w najnowszej konfiguracji
                var updatedSet = Plugin.PackRewardsConfig.RewardSets
                                    .FirstOrDefault(rs => rs.Name == selectedRewardSet.Name);

                if (updatedSet != null)
                {
                    // Aktualizacja ItemsSource dla PackRewardItemsDataGrid z przedmiotami z zaktualizowanego zestawu
                    PackRewardItemsDataGrid.ItemsSource = updatedSet.Items;
                }
                else
                {
                    // Jeśli nie znaleziono zestawu (co może się zdarzyć, jeśli zestaw został usunięty z konfiguracji),
                    // możesz oczyścić ItemsSource dla PackRewardItemsDataGrid
                    PackRewardItemsDataGrid.ItemsSource = null;
                }
            }
        }

        private void AddItemButton_Click(object sender, RoutedEventArgs e)
        {

            var newItem = new PackRewardItem();
            var selectedRewardSet = RewardSetsDataGrid.SelectedItem as RewardSet;
            selectedRewardSet?.Items.Add(newItem);

            PackRewardItemsDataGrid.Items.Refresh();
        }

        private void DeleteItemButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedRewardSet = RewardSetsDataGrid.SelectedItem as RewardSet;
            var selectedItem = PackRewardItemsDataGrid.SelectedItem as PackRewardItem;

            if (selectedRewardSet != null && selectedItem != null)
            {
                selectedRewardSet.Items.Remove(selectedItem);
                PackRewardItemsDataGrid.Items.Refresh();
            }
        }

        private void AddPackButton_Click(object sender, RoutedEventArgs e)
        {
            var newRewardSet = new RewardSet
            {
                Name = "New Pack",
                CostInPoints = 0,
                Items = new List<PackRewardItem>()
            };

            Plugin.PackRewardsConfig.RewardSets.Add(newRewardSet);
            RewardSetsDataGrid.Items.Refresh();
        }

        private void DeletePackButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedRewardSet = RewardSetsDataGrid.SelectedItem as RewardSet;
            if (selectedRewardSet != null)
            {
                Plugin.PackRewardsConfig.RewardSets.Remove(selectedRewardSet);
                RewardSetsDataGrid.Items.Refresh();
            }
        }

    }
}
