using Microsoft.AspNetCore.Http;

namespace Behind.Common.WebApi.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, CorrelationIdOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request);

        context.Features.Set(new CorrelationIdFeature(correlationId));
        context.Response.Headers[options.HeaderName] = correlationId;

        await next(context);
    }

    private string ResolveCorrelationId(HttpRequest request) =>
        request.Headers[options.HeaderName].FirstOrDefault()?.Trim() is { } value && IsValid(value)
            ? value
            : options.Generator();

    private bool IsValid(string value) =>
        value.Length > 0
        && value.Length <= options.MaxLength
        && value.All(IsAllowedCharacter);

    private static bool IsAllowedCharacter(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':';
}
