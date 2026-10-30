using System.ComponentModel.DataAnnotations;
using static Behind.Common.FunctionalExtensions;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SignInEndpoints
{
    public sealed record PasswordRequest(
        [Required, DataType(DataType.EmailAddress)]
        string Email,
        [Required] string Password);

    public sealed record SignInResponse(string AccessToken, DateTimeOffset Expires, string RefreshToken);

    [IgnoreETag]
    public static async Task<IResult> SignInPassword(UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration, ISessionCommands sessionCommands,
        HttpContext httpContext, PasswordRequest request)
    {
        return await securityConfiguration.Value.Jwt.Match(
            none: () => Task.FromResult(Results.InternalServerError()),
            async jwtSettings =>
            {
                var user = await userManager.FindByEmailAsync(request.Email);
                if (user is null || user.Password?.Verify(request.Password) != true)
                {
                    return Results.Unauthorized();
                }

                var humanUser = new HumanUser(user.DisplayName, user.Emails, None);
                var accessToken = AccessToken.Create(humanUser);
                var refreshToken = RefreshToken.Create(humanUser);

                var sessionId = await sessionCommands.CreateForUserId(
                    user.Id,
                    new ValidSession(
                        accessToken,
                        refreshToken,
                        httpContext.ClientAddress(RemoteAddress.MaxLength),
                        httpContext.ClientUserAgent(UserAgent.MaxLength)),
                    httpContext.RequestAborted);

                return Results.Json(new SignInResponse(
                    accessToken.ToJwtToken(user, sessionId, jwtSettings),
                    accessToken.ExpiresAt,
                    refreshToken.ToJwtToken(user, sessionId, jwtSettings)
                ));
            });
    }
}