namespace Behind.Common.Database;

public interface ISoftDeleteContext
{
    SoftDeleteSwitch SoftDelete { get; }

    HardDeleteRegistry HardDeletes { get; }
}
