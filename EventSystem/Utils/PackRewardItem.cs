using ProtoBuf;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace EventSystem.Utils
{
    [ProtoContract]
    public class PackRewardItem : IRewardItem
    {
        [ProtoMember(1)]
        public string ItemTypeId { get; set; }

        [ProtoMember(2)]
        public string ItemSubtypeId { get; set; }

        [ProtoMember(3)]
        public int Amount { get; set; }

        [ProtoMember(4)]
        public double ChanceToDrop { get; set; }

        [XmlIgnore]
        public List<string> AvailableSubTypeIds { get; set; } = new List<string>();

        public PackRewardItem()
        {
            ItemTypeId = string.Empty;
            ItemSubtypeId = string.Empty;
            Amount = 0;
            ChanceToDrop = 100; // Domyślna wartość, można dostosować
        }
    }
}
