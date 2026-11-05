using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Logging;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
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

    public sealed record ConfirmEmailRequest([property: Required] Guid UserId, [property: Required] string Token);

    public sealed record ResendConfirmationRequest(
        [property: Required, DataType(DataType.EmailAddress)] string Email,
        CaptchaResponse? Captcha = null) : ICaptchaRequest;

    public const string ConfirmEmailRoute = "/sign/up/confirm";
    public const string ResendConfirmationRoute = "/sign/up/confirm/resend";

    [IgnoreETag]
    public static async Task<IResult> SignUpPassword(
        UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration,
        IEmailSender<ApplicationUser> emailSender,
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

        var user = new ApplicationUser
        {
            DisplayName = request.DisplayName.ToDisplayName(),
            Emails = [new Email(request.Email, confirmationEnabled ? null : timeProvider.GetUtcNow())]
        };
        
        var result = await userManager.CreateAsync(user, request.Password);

        if (result.Succeeded)
        {
            logger.LogInformation("User {UserId} signed up with a password", () => [user.Id]);

            if (confirmationEnabled)
            {
                await SendConfirmation(userManager, emailSender, configuration, httpContext, logger, user, request.Email);
            }
            else
            {
                logger.LogInformation(
                    "Email of user {UserId} confirmed at sign-up: email confirmation is disabled",
                    () => [user.Id]);
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
        ILogger<SignUpEndpoints> logger,
        ConfirmEmailRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());

        if (user is null)
        {
            logger.LogWarning("Email confirmation rejected: user {UserId} does not exist", () => [request.UserId]);

            return Results.BadRequest();
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);

        if (result.Succeeded)
        {
            logger.LogInformation("Email of user {UserId} confirmed", () => [user.Id]);

            return Results.NoContent();
        }

        logger.LogWarning(
            "Email confirmation of user {UserId} failed with {ErrorCodes}",
            () => [user.Id, result.Errors.Select(e => e.Code).ToArray()]);

        return Results.BadRequest();
    }

    [IgnoreETag]
    public static async Task<IResult> ResendConfirmation(
        UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration,
        IEmailSender<ApplicationUser> emailSender,
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
        else if (await userManager.IsEmailConfirmedAsync(user))
        {
            logger.LogDebug(
                "Confirmation email for user {UserId} not sent: the email is already confirmed",
                () => [user.Id]);
        }
        else
        {
            await SendConfirmation(userManager, emailSender, configuration, httpContext, logger, user, request.Email);
        }

        return Results.NoContent();
    }

    private static async Task SendConfirmation(
        UserManager<ApplicationUser> userManager,
        IEmailSender<ApplicationUser> emailSender,
        IConfiguration configuration,
        HttpContext httpContext,
        ILogger<SignUpEndpoints> logger,
        ApplicationUser user,
        string email)
    {
        try
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var link = configuration.FrontendUri()
                .GetOrElse(() => httpContext.BaseUri())
                .ToFrontendLink(
                    ConfirmEmailRoute,
                    [new("userId", user.Id.ToString()), new("token", token)]);

            await emailSender.SendConfirmationLinkAsync(user, email, link);

            logger.LogInformation("Confirmation email sent to user {UserId}", () => [user.Id]);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to send a confirmation email to user {UserId}", () => [user.Id]);
        }
    }
}
