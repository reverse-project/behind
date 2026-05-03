using System.Text.Json;
using Behind.Common.WebApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OtpNet;

namespace Behind.Core.WebApi.Endpoints;

public static class ConfigEndpoint
{
    public static IResult ExecuteGet(IConfiguration config, [FromQuery] int totp)
    {
        try
        {
            var totpVerifier = new Totp(Base32Encoding.ToBytes(config["TotpSecretKey"]));

            var options = new JsonSerializerOptions 
            {
                WriteIndented = true 
            };
            
            return
                !totpVerifier.VerifyTotp(
                    TimeProvider.System.GetUtcNow().DateTime,
                    totp.ToString(),
                    out _,
                    new VerificationWindow()
                )
                    ? Results.StatusCode(403)
                    : Results.Json(config.Serialize(), options);
        }
        catch
        {
            return Results.StatusCode(403);
        }
    }
}