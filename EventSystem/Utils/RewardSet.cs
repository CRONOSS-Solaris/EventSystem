using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace EventSystem.Utils
{
    [ProtoContract]
    public class RewardSet
    {
        [ProtoMember(1)]
        public string Name { get; set; }

        [ProtoMember(2)]
        public List<RewardItem> Items { get; set; } = new List<RewardItem>();

        [ProtoMember(3)]
        public int CostInPoints { get; set; }
    }

    [ProtoContract]
    public class RewardItem
    {
        [ProtoMember(1)]
        public string ItemTypeId { get; set; }

        [ProtoMember(2)]
        public string ItemSubtypeId { get; set; }

        [ProtoMember(3)]
        public int Amount { get; set; }

        [ProtoMember(4)]
        public double ChanceToDrop { get; set; }

        [ProtoMember(5)]
        public int CostInPoints { get; set; }

        [XmlIgnore]
        public List<string> AvailableSubTypeIds { get; set; } = new List<string>();

        public RewardItem()
        {
            // Domyślne wartości
            ItemTypeId = string.Empty;
            ItemSubtypeId = string.Empty;
            Amount = 1; // Domyślna ilość przedmiotu
            ChanceToDrop = 100; // Domyślna szansa na otrzymanie przedmiotu
        }
    }
}
