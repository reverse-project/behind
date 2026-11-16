using Microsoft.AspNetCore.Http;

namespace Behind.Common.WebApi.Middleware;

public sealed record ETagOptions
{
    public static readonly ETagOptions Default = new();

    public IReadOnlySet<string> AllowedMethods { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { HttpMethods.Get, HttpMethods.Head };

    public IReadOnlySet<int> AllowedStatusCodes { get; init; } =
        new HashSet<int> { StatusCodes.Status200OK };
}
