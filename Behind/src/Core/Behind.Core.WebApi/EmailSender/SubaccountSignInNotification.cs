using System.Net;
using Behind.Common;
using Behind.Common.Templating;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.EmailSender;

internal static class SubaccountSignInNotification
{
    internal static Task AfterSessionCommit(HttpContext http, ApplicationUser user, Guid? switchedFamilyId, ILogger logger)
    {
        var transaction = http.Features.Get<AuthenticationTransaction>();
        if (transaction is null)
        {
            return Send(http, user, switchedFamilyId, logger);
        }

        transaction.OnCommittedAsync(() => Send(http, user, switchedFamilyId, logger));
        return Task.CompletedTask;
    }

    private static async Task Send(HttpContext http, ApplicationUser user, Guid? switchedFamilyId, ILogger logger)
    {
        try
        {
            var scopeFactory = http.RequestServices?.GetService<IServiceScopeFactory>();
            if (scopeFactory is null)
            {
                return;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<AbstractCoreContext>();
            var cancellationToken = http.RequestAborted;
            var primaryId = await SubaccountEndpoints.FindPrimaryUserId(db, user.Id, cancellationToken);
            if (primaryId is null || (switchedFamilyId.HasValue && primaryId != switchedFamilyId))
            {
                return;
            }

            var addresses = await services.GetRequiredService<IEmailQueries>().Addresses(primaryId.Value, cancellationToken);
            var recipient = addresses.FirstOrDefault(address =>
                address.ConfirmedAt is not null && !string.IsNullOrWhiteSpace(address.Value));
            if (recipient is null)
            {
                return;
            }

            var activity = switchedFamilyId.HasValue ? "Switched into a linked subaccount" : "Signed in to a linked subaccount";
            var html = DefaultEmailTemplates.SubaccountSignInEmail
                .AddParameter(new Parameter("Activity", activity))
                .AddParameter(new Parameter("Account", WebUtility.HtmlEncode(user.Id.ToUserLabel(user.DisplayName))))
                .Render();

            var sender = services.GetRequiredService<IEmailSender>();
            if (sender is EmailSender configured)
            {
                await configured.SendDeliveryEmailAsync(recipient.Value, activity, html, cancellationToken);
            }
            else
            {
                await sender.SendEmailAsync(recipient.Value, activity, html);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to send linked subaccount sign-in notification for user {UserId}", user.Id);
        }
    }
}
