using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;

namespace Behind.Sport.Database;

public sealed record SportFilter(
    Option<Guid> Discipline,
    Option<Guid> Season,
    Option<Guid> Team,
    Option<Guid> Player,
    Option<Guid> Position,
    Option<Guid> StatType,
    Option<StatScope> Scope)
{
    public static SportFilter Empty { get; } = new(default, default, default, default, default, default, default);
}

public sealed record ListRequest(
    Flipper Flipper,
    Option<string> Search,
    Option<string> Letter,
    DeletionState State,
    SportFilter Filter);
