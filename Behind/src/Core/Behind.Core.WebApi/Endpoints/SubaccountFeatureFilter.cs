using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SubaccountFeatureFilter(bool requiresLinking) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        var settings = http.RequestServices.GetRequiredService<IOptions<SecurityConfiguration>>().Value.Accounts;
        if (!settings.Subaccounts || (requiresLinking && !settings.LinkUnlink))
        {
            return ValueTask.FromResult<object?>(Results.NotFound());
        }
        if (context.Arguments.OfType<SubaccountEndpoints.IPinRequest>().FirstOrDefault() is { } request)
        {
            var maxLength = settings.SwitchPinMaxLength is >= 1 and <= SecurityAccountSettings.DefaultSwitchPinMaxLength
                ? settings.SwitchPinMaxLength
                : SecurityAccountSettings.DefaultSwitchPinMaxLength;
            var errors = new Dictionary<string, string[]>();
            if (request.Pin?.Length > maxLength)
            {
                errors.Add(nameof(request.Pin), [$"The PIN must be at most {maxLength} characters."]);
            }
            if (request.PrimaryPin?.Length > maxLength)
            {
                errors.Add(nameof(request.PrimaryPin), [$"The primary PIN must be at most {maxLength} characters."]);
            }
            if (errors.Count > 0)
            {
                return ValueTask.FromResult<object?>(Results.ValidationProblem(errors));
            }
        }
        return next(context);
    }
}
