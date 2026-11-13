using System.Data;
using System.Net;
using System.Net.Mail;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi;

public sealed class PrivateMessageEmailDispatcher(
    AbstractCoreContext context, PrivateMessageEmailStore deliveries, PrivateMessageStore messages,
    IModuleEmailSender sender, IConfiguration configuration, ILogger<PrivateMessageEmailDispatcher> logger)
{
    public async Task Deliver(CancellationToken cancellationToken)
    {
        var pending = await deliveries.Pending(cancellationToken);
        if (pending.Count == 0)
        {
            return;
        }
        var frontend = configuration.FrontendUri().Match<Uri?>(() => null, uri => uri);
        if (frontend is null || !frontend.IsAbsoluteUri || frontend.Scheme is not ("https" or "http"))
        {
            logger.LogError("Private message emails remain pending: Frontend:Url must be an absolute HTTP(S) URL");
            return;
        }
        foreach (var delivery in pending)
        {
            var claimed = await Claim(delivery.Id.Key, cancellationToken);
            if (claimed is null)
            {
                continue;
            }
            var link = frontend.ToFrontendLink($"/private-messages/{claimed.ConversationId}", []);
            try
            {
                await sender.SendEmailAsync(claimed.Email, "New private message",
                    $"<p>You have a new private message.</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Open private message</a></p>",
                    cancellationToken);
            }
            catch (SmtpException exception) when (exception.StatusCode != SmtpStatusCode.GeneralFailure)
            {
                logger.LogError(exception, "Private message email {DeliveryId} was rejected; attempt {Attempt} of 3",
                    claimed.Id.Key, claimed.Attempts);
                await Save(claimed, "failed", cancellationToken);
                continue;
            }
            await Save(claimed, "sent", cancellationToken);
        }
    }

    private async Task<PrivateMessageEmailDelivery?> Claim(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var delivery = await deliveries.Find(id, cancellationToken);
        if (delivery is null || delivery.Status is "sending" or "sent" or "cancelled" || delivery.Attempts >= 3)
        {
            return null;
        }
        if (delivery.Status is not ("pending" or "failed"))
        {
            throw new InvalidOperationException("Invalid private message email delivery state.");
        }
        if (!await context.Users.AnyAsync(user => EF.Property<Guid>(user, "Id") == delivery.UserId, cancellationToken)
            || await messages.FindEvidence(delivery.UserId, delivery.MessageId, cancellationToken: cancellationToken) is null)
        {
            await Save(delivery, "cancelled", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var claimed = delivery with { Id = new(id, delivery.Id.Version + 1), Status = "sending", Attempts = delivery.Attempts + 1 };
        deliveries.Stage(claimed);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    private async Task Save(PrivateMessageEmailDelivery delivery, string status, CancellationToken cancellationToken)
    {
        deliveries.Stage(delivery with { Id = new(delivery.Id.Key, delivery.Id.Version + 1), Status = status });
        await context.SaveChangesAsync(cancellationToken);
    }
}
