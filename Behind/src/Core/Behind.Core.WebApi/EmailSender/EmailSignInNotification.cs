using System.Net;
using Behind.Common.Templating;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Domain;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.EmailSender;

public sealed record EmailSignInContext(string Email, string RemoteIp, string UserAgent)
{
    internal static EmailSignInContext FromRequest(string email, HttpContext http) =>
        new(email, http.ClientAddress(RemoteAddress.MaxLength), http.ClientUserAgent(Behind.Core.Domain.UserAgent.MaxLength));
}

internal static class EmailSignInNotification
{
    private const string Subject = "New sign-in to your account";

    internal static Task AfterSessionCommit(HttpContext http, Guid userId, EmailSignInContext signIn, ILogger logger)
    {
        var transaction = http.Features.Get<AuthenticationTransaction>();
        if (transaction is null)
        {
            return Send(http, userId, signIn, logger);
        }

        transaction.OnCommittedAsync(() => Send(http, userId, signIn, logger));
        return Task.CompletedTask;
    }

    private static async Task Send(HttpContext http, Guid userId, EmailSignInContext signIn, ILogger logger)
    {
        try
        {
            var services = http.RequestServices;
            if (services is null)
            {
                return;
            }

            var sender = services.GetService<IEmailSender>();
            if (sender is null)
            {
                return;
            }

            var timeoutMilliseconds = services
                .GetRequiredService<IOptionsSnapshot<SecurityConfiguration>>()
                .Value.SignInNotificationTimeoutMilliseconds;
            using var timeout = new CancellationTokenSource(timeoutMilliseconds);

            var html = DefaultEmailTemplates.EmailSignInAlert
                .AddParameter(new Parameter("RemoteIp", WebUtility.HtmlEncode(signIn.RemoteIp)))
                .AddParameter(new Parameter("UserAgent", WebUtility.HtmlEncode(signIn.UserAgent)))
                .Render();

            if (sender is EmailSender configured)
            {
                await configured.SendDeliveryEmailAsync(signIn.Email, Subject, html, timeout.Token).WaitAsync(timeout.Token);
            }
            else
            {
                await sender.SendEmailAsync(signIn.Email, Subject, html).WaitAsync(timeout.Token);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to send email sign-in notification for user {UserId}", userId);
        }
    }
}
