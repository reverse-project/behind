using System.Text.Json;
using Behind.Common;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OtpNet;
using Totp = OtpNet.Totp;
using TotpValue = Behind.Core.Common.Configuration.Totp;

namespace Behind.Core.WebApi.Endpoints;

public static class ConfigEndpoint
{
    public static IResult ExecuteGet(IConfiguration configuration, IOptions<SecurityConfiguration> securityConfig,
        [FromQuery] int totp)
    {
        try
        {
            return securityConfig.Value.SecretKey.Match(
                none: () => Results.StatusCode(403),
                secretKey =>
                {
                    if (secretKey is not TotpValue totpValue)
                    {
                        return Results.StatusCode(403);
                    }

                    var totpVerifier = new Totp(Base32Encoding.ToBytes(totpValue.Value));

                    if (!totpVerifier.VerifyTotp(
                            TimeProvider.System.GetUtcNow().DateTime,
                            totp.ToString(),
                            out _,
                            new VerificationWindow()
                        ))
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
                                .MaskNestedFields(new[] { "Security", "Jwt" }, "SigningKey"),
                            options);
                    }
                });
            
        }
        catch
        {
            return Results.StatusCode(403);
        }
    }
}