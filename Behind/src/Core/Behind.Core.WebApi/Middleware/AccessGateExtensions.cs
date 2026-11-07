using Behind.Core.Domain;
using Behind.Core.WebApi.Captcha;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Middleware;

public static class AccessGateExtensions
{
    public const string RestrictionRoute = "/access/restriction";

    private static readonly IReadOnlyList<PathString> OpenPaths =
    [
        "/sign",
        "/session",
        "/accounts",
        "/locales",
        "/about",
        ModuleEndpoints.Route,
        "/profile",
        CaptchaExtensions.ChallengeRoute,
        VfsTypes.AccessImageUrl,
        RestrictionRoute
    ];

    extension(PathString path)
    {
        public bool IsAlwaysOpen => OpenPaths.Any(open => path.StartsWithSegments(open));
    }

    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseAccessGate() => app.UseMiddleware<AccessGateMiddleware>();
    }
}
