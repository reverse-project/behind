using Behind.Common.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Behind.Common.WebApi.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    CorrelationIdOptions options,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request);

        context.Features.Set(new CorrelationIdFeature(correlationId));
        context.Response.Headers[options.HeaderName] = correlationId;

        using var _ = CorrelationContext.Begin(correlationId);

        await next(context);
    }

    private string ResolveCorrelationId(HttpRequest request)
    {
        var supplied = request.Headers[options.HeaderName].FirstOrDefault()?.Trim();

        if (supplied is not null && IsValid(supplied))
        {
            logger.LogDebug(
                "Reusing correlation id {CorrelationId} from {HeaderName}",
                () => [supplied, options.HeaderName]);

            return supplied;
        }

        var generated = options.Generator();

        logger.Log(
            supplied is null ? LogLevel.Debug : LogLevel.Warning,
            "Generated correlation id {CorrelationId} because {HeaderName} was {HeaderState}",
            () => [generated, options.HeaderName, supplied is null ? "missing" : "invalid"]);

        return generated;
    }

    private bool IsValid(string value) =>
        value.Length > 0
        && value.Length <= options.MaxLength
        && value.All(IsAllowedCharacter);

    private static bool IsAllowedCharacter(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':';
}
