using EventSystem.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch;

namespace EventSystem.Config
{
    public class ItemRewardsConfig : ViewModel
    {
        private List<IndividualRewardItem> _individualItems = new List<IndividualRewardItem>();

        public List<IndividualRewardItem> IndividualItems
        {
            get => _individualItems;
            set => SetValue(ref _individualItems, value);
        }

        // Metoda do generowania przykładowych indywidualnych przedmiotów
        public void GenerateExampleIndividualItems()
        {
            if (_individualItems.Count == 0)
            {
                _individualItems.Add(new IndividualRewardItem
                {
                    ItemTypeId = "MyObjectBuilder_Ingot",
                    ItemSubtypeId = "Silver",
                    Amount = 10,
                    CostInPoints = 50
                });

                _individualItems.Add(new IndividualRewardItem
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
