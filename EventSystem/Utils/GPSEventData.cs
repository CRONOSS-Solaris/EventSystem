using ProtoBuf;
using System;
using VRageMath;

[Serializable]
[ProtoContract]
public class GPSEventData
{
    [ProtoMember(1)]
    public long PlayerId { get; set; }
    [ProtoMember(2)]
    public string Name { get; set; }
    [ProtoMember(3)]
    public Vector3D Coords { get; set; }
    [ProtoMember(4)]
    public string Description { get; set; }
    [ProtoMember(5)]
    public TimeSpan? DiscardAt { get; set; }
    [ProtoMember(6)]
    public bool ShowOnHud { get; set; } = true;
    [ProtoMember(7)]
    public bool AlwaysVisible { get; set; } = true;
    [ProtoMember(8)]
    public Color? Color { get; set; }
    [ProtoMember(9)]
    public long EntityId { get; set; } = 0;
    [ProtoMember(10)]
    public bool IsObjective { get; set; } = false;
    [ProtoMember(11)]
    public long ContractId { get; set; } = 0;
}
