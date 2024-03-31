using ProtoBuf;

[ProtoContract]
public interface IRewardItem
{
    [ProtoMember(1)]
    string ItemTypeId { get; }

    [ProtoMember(2)]
    string ItemSubtypeId { get; }

    [ProtoMember(3)]
    int Amount { get; }
}
