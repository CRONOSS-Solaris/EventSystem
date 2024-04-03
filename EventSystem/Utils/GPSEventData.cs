using System;
using VRageMath;

[Serializable]
public class GPSEventData
{
    public long PlayerId { get; set; }
    public string Name { get; set; }
    public Vector3D Coords { get; set; }
    public string Description { get; set; }
    public TimeSpan? DiscardAt { get; set; }
    public bool ShowOnHud { get; set; } = true;
    public bool AlwaysVisible { get; set; } = true;
    public Color? Color { get; set; }
    public long EntityId { get; set; } = 0;
    public bool IsObjective { get; set; } = false;
    public long ContractId { get; set; } = 0;
}
