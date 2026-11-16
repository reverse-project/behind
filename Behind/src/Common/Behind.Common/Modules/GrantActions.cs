namespace Behind.Common.Modules;

public static class GrantActions
{
    public const string Read = nameof(Read);
    public const string Write = nameof(Write);
    public const string Delete = nameof(Delete);
    public const string Manage = nameof(Manage);

    public static readonly IReadOnlyList<string> All = [Read, Write, Delete, Manage];
}
