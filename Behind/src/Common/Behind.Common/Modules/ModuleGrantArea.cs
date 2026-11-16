namespace Behind.Common.Modules;

public sealed record ModuleGrantArea(string Area, ModuleAudience Audience, IReadOnlyList<string> Actions)
{
    public ModuleGrantArea(string area, ModuleAudience audience)
        : this(area, audience, GrantActions.All)
    {
    }
}
