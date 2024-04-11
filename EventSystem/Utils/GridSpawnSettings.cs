using VRage.Game;

public class GridSpawnSettings
{
    public FunctionalBlockSettings FunctionalBlockSettings { get; set; } = new FunctionalBlockSettings();
    public CubeGridSettings CubeGridSettings { get; set; } = new CubeGridSettings();
    public GridOwnerSettings GridOwnerSettings { get; set; } = new GridOwnerSettings();
}

public class FunctionalBlockSettings
{
    public bool Enabled { get; set; }
    public MyOwnershipShareModeEnum ShareMode { get; set; } = MyOwnershipShareModeEnum.None;
}

public class CubeGridSettings
{
    public bool DampenersEnabled { get; set; }
    public bool Editable { get; set; }
    public bool IsPowered { get; set; }
    public bool IsStatic { get; set; }
    public bool DestructibleBlocks { get; set; }

}

public class GridOwnerSettings
{
    public long OwnerGrid { get; set;}
}