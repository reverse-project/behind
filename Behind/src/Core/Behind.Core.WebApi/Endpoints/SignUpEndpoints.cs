using System.ComponentModel.DataAnnotations;
using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Captcha;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SignUpEndpoints
{
    public sealed record PasswordRequest([property: Required, DataType(DataType.EmailAddress)] string Email,
        [property: Required] string Password,
        [property: MaxLength(DisplayNameExtensions.MaxLength)] string? DisplayName = null,
        CaptchaResponse? Captcha = null) : ICaptchaRequest;

    public sealed record ConfirmEmailRequest(
        [property: Required] Guid UserId, [property: Required] string Token, Guid EmailId = default);

    public sealed record ResendConfirmationRequest(
        [property: Required, DataType(DataType.EmailAddress)] string Email,
        CaptchaResponse? Captcha = null) : ICaptchaRequest;

    public const string ConfirmEmailRoute = "/sign/up/confirm";
    public const string ResendConfirmationRoute = "/sign/up/confirm/resend";

    [IgnoreETag]
    public static async Task<IResult> SignUpPassword(
        UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration,
        IAccessSettingsQueries accessSettings,
        IEmailSender<ApplicationUser> emailSender,
        IEmailQueries emailQueries,
        IConfiguration configuration,
        TimeProvider timeProvider,
        HttpContext httpContext,
        ILogger<SignUpEndpoints> logger,
        PasswordRequest request)
    {
        var confirmationEnabled = securityConfiguration.Value.EmailConfirmation;

        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            logger.LogWarning("Sign-up rejected: the email is already in use");

            return Results.Conflict("Email already in use");
        }

        var moderated = (await accessSettings.Current(httpContext.RequestAborted)).ModeratedSignUp;

        var user = new ApplicationUser
        {
            DisplayName = request.DisplayName.ToDisplayName(),
            Emails = [new Email(request.Email.Trim().ToLowerInvariant(), confirmationEnabled ? null : timeProvider.GetUtcNow())],
            PendingApproval = moderated
        };
        
        var result = await userManager.CreateAsync(user, request.Password);

        if (result.Succeeded)
        {
            logger.LogInformation("User {UserId} signed up with a password", () => [user.Id]);

            if (confirmationEnabled)
            {
                var address = (await emailQueries.Addresses(user.Id, httpContext.RequestAborted))
                    .Single(item => item.Value == request.Email.Trim().ToLowerInvariant());
                await userManager.SendAddressConfirmation(user, address, emailSender, configuration, logger);
            }
            else
            {
                logger.LogInformation(
                    "Email of user {UserId} confirmed at sign-up: email confirmation is disabled",
                    () => [user.Id]);
            }

            if (moderated)
            {
                logger.LogInformation("User {UserId} is waiting for approval", () => [user.Id]);

                return Results.Json(
                    new SignInEndpoints.ApprovalPendingResponse(),
                    statusCode: StatusCodes.Status202Accepted);
            }

            return Results.StatusCode(204);
        }

        logger.LogWarning(
            "Sign-up of user {UserId} failed with {ErrorCodes}",
            () => [user.Id, result.Errors.Select(e => e.Code).ToArray()]);

        return Results.BadRequest(result.Errors.Select(e => e.Description));
    }

    [IgnoreETag]
    public static async Task<IResult> ConfirmEmail(
        UserManager<ApplicationUser> userManager,
        IEmailQueries emailQueries,
        IEmailCommands emailCommands,
        ILogger<SignUpEndpoints> logger,
        ConfirmEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());

        if (user is null)
        {
            logger.LogWarning("Email confirmation rejected: user {UserId} does not exist", () => [request.UserId]);

            return Results.BadRequest();
        }

        var address = (await emailQueries.Addresses(user.Id, cancellationToken)).SingleOrDefault(item => item.Id == request.EmailId);
        var valid = address is not null && await userManager.VerifyUserTokenAsync(
            user, userManager.Options.Tokens.EmailConfirmationTokenProvider, address.ConfirmationPurpose, request.Token);

        if (valid && address is not null
            && await emailCommands.ConfirmAddress(user.Id, address.Id, address.Value, cancellationToken))
        {
            logger.LogInformation("Email of user {UserId} confirmed", () => [user.Id]);

            return Results.NoContent();
        }

        logger.LogWarning("Email confirmation of user {UserId} rejected: address or token is invalid", () => [user.Id]);

        return Results.BadRequest();
    }

    [IgnoreETag]
    public static async Task<IResult> ResendConfirmation(
        UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration,
        IEmailSender<ApplicationUser> emailSender,
        IEmailQueries emailQueries,
        IConfiguration configuration,
        HttpContext httpContext,
        ILogger<SignUpEndpoints> logger,
        ResendConfirmationRequest request)
    {
        if (!securityConfiguration.Value.EmailConfirmation)
        {
            logger.LogWarning("Confirmation email resend rejected: email confirmation is disabled");

            return Results.NotFound();
        }

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            logger.LogDebug("Confirmation email requested for an unknown email, nothing sent");
        }
        else
        {
            var address = (await emailQueries.Addresses(user.Id, httpContext.RequestAborted))
                .SingleOrDefault(item => string.Equals(item.Value, request.Email.Trim(), StringComparison.OrdinalIgnoreCase));

            if (address is not null && address.ConfirmedAt is null)
            {
                await userManager.SendAddressConfirmation(user, address, emailSender, configuration, logger);
            }
            else
            {
                logger.LogDebug("Confirmation email for user {UserId} not sent: no unconfirmed address", () => [user.Id]);
            }
        }

        return Results.NoContent();
    }

}
