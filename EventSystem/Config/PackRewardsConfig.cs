using EventSystem.Utils;
using System.Collections.Generic;
using Torch;

namespace EventSystem.Config
{
    public class PackRewardsConfig : ViewModel
    {
        private List<RewardSet> _rewardSets = new List<RewardSet>();

        public List<RewardSet> RewardSets
        {
            get => _rewardSets;
            set => SetValue(ref _rewardSets, value);
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
                    Items = new List<PackRewardItem>
                    {
                        new PackRewardItem { ItemTypeId = "MyObjectBuilder_Ingot", ItemSubtypeId = "Iron", Amount = 100, ChanceToDrop = 100 },
                        new PackRewardItem{ ItemTypeId = "MyObjectBuilder_Component", ItemSubtypeId = "Construction", Amount = 50, ChanceToDrop = 100 }
                    }
                });

                _rewardSets.Add(new RewardSet
                {
                    Name = "AdvancedPack",
                    CostInPoints = 500,
                    Items = new List<PackRewardItem>
                    {
                        new PackRewardItem { ItemTypeId = "MyObjectBuilder_Ingot", ItemSubtypeId = "Uranium", Amount = 5, ChanceToDrop = 50 },
                        new PackRewardItem { ItemTypeId = "MyObjectBuilder_Component", ItemSubtypeId = "Detector", Amount = 10, ChanceToDrop = 75 }
                    }
                });
            }
        }
    }
}
