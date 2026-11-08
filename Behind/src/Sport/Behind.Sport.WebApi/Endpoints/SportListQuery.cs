using Behind.Common;
using Behind.Sport.Database;
using Behind.Sport.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Sport.WebApi.Endpoints;

public sealed record SportListQuery(
    [FromQuery] int? Page = null,
    [FromQuery] int? PageSize = null,
    [FromQuery] string? Search = null,
    [FromQuery] string? Letter = null,
    [FromQuery] bool Deleted = false,
    [FromQuery] Guid? Discipline = null,
    [FromQuery] Guid? Season = null,
    [FromQuery] Guid? Team = null,
    [FromQuery] Guid? Player = null,
    [FromQuery] Guid? Position = null,
    [FromQuery] Guid? StatType = null,
    [FromQuery] StatScope? Scope = null)
{
    public SportFilter ToFilter() =>
        new(
            Discipline.ToOption(),
            Season.ToOption(),
            Team.ToOption(),
            Player.ToOption(),
            Position.ToOption(),
            StatType.ToOption(),
            Scope.ToOption());
}
