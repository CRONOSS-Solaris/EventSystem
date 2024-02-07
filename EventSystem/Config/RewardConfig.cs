using EventSystem.Utils;
using System.Collections.Generic;
using Torch;

namespace EventSystem.Config
{
    public class RewardConfig : ViewModel
    {
        private List<RewardSet> _rewardSets = new List<RewardSet>();
        private List<RewardItem> _individualItems = new List<RewardItem>();

        public List<RewardSet> RewardSets
        {
            get => _rewardSets;
            set => SetValue(ref _rewardSets, value);
        }

        public List<RewardItem> IndividualItems
        {
            get => _individualItems;
            set => SetValue(ref _individualItems, value);
        }

        // Metoda do generowania przykładowych zestawów nagród
        public void GenerateExampleRewards()
        {
            if (_rewardSets.Count == 0)
            {
                _rewardSets.Add(new RewardSet
                {
                    Name = "StarterPack",
                    CostInPoints = 100,
                    Items = new List<RewardItem>
                    {
                        new RewardItem { ItemTypeId = "MyObjectBuilder_Ingot", ItemSubtypeId = "Iron", Amount = 100, ChanceToDrop = 100 },
                        new RewardItem { ItemTypeId = "MyObjectBuilder_Component", ItemSubtypeId = "Construction", Amount = 50, ChanceToDrop = 100 }
                    }
                });

                _rewardSets.Add(new RewardSet
                {
                    Name = "AdvancedPack",
                    CostInPoints = 500,
                    Items = new List<RewardItem>
                    {
                        new RewardItem { ItemTypeId = "MyObjectBuilder_Ingot", ItemSubtypeId = "Uranium", Amount = 5, ChanceToDrop = 50 },
                        new RewardItem { ItemTypeId = "MyObjectBuilder_Component", ItemSubtypeId = "Detector", Amount = 10, ChanceToDrop = 75 }
                    }
                });
            }
        }

        public void GenerateExampleIndividualItems()
        {
            if (_individualItems.Count == 0)
            {
                _individualItems.Add(new RewardItem
                {
                    ItemTypeId = "MyObjectBuilder_Ingot",
                    ItemSubtypeId = "Silver",
                    Amount = 10,
                    CostInPoints = 50
                });

                _individualItems.Add(new RewardItem
                {
                    ItemTypeId = "MyObjectBuilder_Ore",
                    ItemSubtypeId = "Gold",
                    Amount = 5,
                    CostInPoints = 75
                });
            }
        }
    }
}
