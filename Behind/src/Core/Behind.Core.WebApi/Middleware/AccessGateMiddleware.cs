using Behind.Common.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Middleware;

public sealed class AccessGateMiddleware(RequestDelegate next, ILogger<AccessGateMiddleware> logger)
{
    public const string RestrictedCode = "restricted";

    public sealed record RestrictedResponse(string Code = RestrictedCode);

    public async Task InvokeAsync(HttpContext context, AccessGate gate)
    {
        if (context.Request.Path.IsAlwaysOpen)
        {
            await next(context);
            return;
        }

        var decision = await gate.Decide(context);

        if (decision is AccessDecision.Restricted)
        {
            logger.LogDebug(
                "Rejected restricted request to {Path}: the visitor does not hold the required role",
                () => [context.Request.Path.Value]);

            context.Response.Headers.CacheControl = "no-store";

            await Results.Json(new RestrictedResponse(), statusCode: StatusCodes.Status403Forbidden)
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}
