using Behind.Common;
using Behind.Common.Logging;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Endpoints;

public static class AddressConfirmationExtensions
{
    extension(IEmailQueries.Address address)
    {
        public string ConfirmationPurpose => $"ConfirmEmail:{address.Id}:{address.Value}";
    }

    extension(UserManager<ApplicationUser> manager)
    {
        public async Task<bool> SendAddressConfirmation(
            ApplicationUser user, IEmailQueries.Address address, IEmailSender<ApplicationUser> sender,
            IConfiguration configuration, HttpContext httpContext, ILogger logger)
        {
            try
            {
                var token = await manager.GenerateUserTokenAsync(
                    user, manager.Options.Tokens.EmailConfirmationTokenProvider, address.ConfirmationPurpose);
                var link = configuration.FrontendUri()
                    .GetOrElse(() => httpContext.BaseUri())
                    .ToFrontendLink(SignUpEndpoints.ConfirmEmailRoute,
                        [new("userId", user.Id.ToString()), new("token", token), new("emailId", address.Id.ToString())]);

                await sender.SendConfirmationLinkAsync(user, address.Value, link);
                logger.LogInformation("Confirmation email sent to user {UserId}", () => [user.Id]);
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Failed to send a confirmation email to user {UserId}", () => [user.Id]);
                return false;
            }
        }
    }
}
