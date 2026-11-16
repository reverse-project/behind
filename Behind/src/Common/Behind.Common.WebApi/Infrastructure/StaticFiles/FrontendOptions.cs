namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public sealed record FrontendOptions
{
    public const string SectionName = "Frontend:StaticFiles";

    public static readonly FrontendOptions Default = new();

    public bool Minify { get; init; } = true;

    public bool Compress { get; init; } = true;

    public string DefaultFile { get; init; } = "index.html";

    public string CacheControl { get; init; } = "no-cache";

    public string ContentSecurityPolicy { get; init; } =
        "default-src 'self'; script-src 'self' 'unsafe-eval'; style-src 'self'; img-src 'self' data:; " +
        "connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    public string ReferrerPolicy { get; init; } = "no-referrer";

    public IReadOnlyList<string> CompressedMimeTypes { get; init; } =
    [
        "text/html",
        "text/css",
        "text/javascript",
        "application/javascript",
        "application/manifest+json",
        "image/svg+xml",
        "text/plain"
    ];
}
