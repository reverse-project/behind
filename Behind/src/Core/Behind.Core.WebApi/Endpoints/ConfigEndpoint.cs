using System.Text.Json;
using Behind.Common;
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
        [FromQuery] int totp)
    {
        try
        {
            if (!securityConfig.Value.VerifyTotp(totp.ToString(), TimeProvider.System.GetUtcNow()))
            {
                return Results.StatusCode(403);
            }

            using (var sensitiveContext = new SensitiveContext())
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                }.WithSensitiveContext(sensitiveContext);

                return Results.Json(
                    configuration.Serialize()
                        .MaskSensitiveConfigurationValues("ConnectionStrings")
                        .MaskFields("Security", "SecretKey")
                        .MaskNestedFields(new[] { "Security", "MagicLink" }, "ActualSecret", "OldSecret")
                        .MaskNestedFields(new[] { "Security", "Jwt" }, "SigningKey")
                        .MaskNestedFields(new[] { "Security", "Elevation" }, "DeveloperSecretKey"),
                    options);
            }
        }
        catch
        {
            return Results.StatusCode(403);
        }
    }
}