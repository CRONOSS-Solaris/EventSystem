using ProtoBuf;
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
        public List<PackRewardItem> Items { get; set; } = new List<PackRewardItem>();

        [ProtoMember(3)]
        public int CostInPoints { get; set; }

        public RewardSet()
        {
            Name = string.Empty;
            CostInPoints = 0; // Domyślna wartość, można dostosować
        }
    }
}
