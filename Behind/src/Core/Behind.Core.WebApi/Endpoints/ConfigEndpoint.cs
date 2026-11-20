using System.Text.Json;
using Behind.Common;
using Behind.Common.Configuration;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public static class ConfigEndpoint
{
    public static IResult ExecuteGet(IConfiguration configuration, IOptions<SecurityConfiguration> securityConfig,
        TimeProvider timeProvider, [FromQuery] string totp)
    {
        try
        {
            if (!securityConfig.Value.VerifyTotp(totp, timeProvider.GetUtcNow()))
            {
                return Results.StatusCode(403);
            }

            using (var sensitiveContext = new SensitiveContext())
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                }.WithSensitiveContext(sensitiveContext);

                var root = configuration.Serialize().AsObject();
                foreach (var field in root.Fields().Where(field => field.Path.IsSensitiveConfigurationPath))
                {
                    field.Value.ReplaceWith(field.Value.ToString().MaskSensitiveValue());
                }

                return Results.Json(root, options);
            }
        }
        catch
        {
            return Results.StatusCode(403);
        }
    }
}