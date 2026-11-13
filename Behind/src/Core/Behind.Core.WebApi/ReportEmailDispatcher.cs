using System.Data;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi;

public sealed class ReportEmailDispatcher(
    AbstractCoreContext context, ReportStore reports, IModuleEmailSender sender,
    TimeProvider clock, ILogger<ReportEmailDispatcher> logger)
{
    public DateTimeOffset UtcNow => clock.GetUtcNow();

    public static string Status(IReadOnlyList<ReportEmailDelivery> deliveries)
    {
        if (deliveries.Any(delivery => delivery.Status is not ("pending" or "sending" or "failed" or "sent")))
        {
            throw new InvalidOperationException("Invalid report delivery state.");
        }

        return deliveries.Count == 0 ? "notRequested"
            : deliveries.Any(delivery => delivery.Status == "sending") ? "sending"
            : deliveries.Any(delivery => delivery.Status == "failed") ? "failed"
            : deliveries.Any(delivery => delivery.Status == "pending") ? "pending" : "sent";
    }

    public async Task<string> Deliver(Guid reportId, CancellationToken cancellationToken)
    {
        var pending = (await reports.Deliveries(reportId, cancellationToken))
            .Where(delivery => delivery.Status is "pending" or "failed").ToArray();
        foreach (var delivery in pending)
        {
            var claimed = await Claim(delivery.Id.Key, reportId, cancellationToken);
            if (claimed is null)
            {
                continue;
            }

            try
            {
                await sender.SendEmailAsync(claimed.Email, "Moderation notification",
                    $"<p>{WebUtility.HtmlEncode(claimed.Body).Replace("\n", "<br>", StringComparison.Ordinal)}</p>", cancellationToken);
            }
            catch (Exception exception) when (exception is SmtpException or SocketException or IOException
                                              or HttpRequestException or InvalidOperationException)
            {
                logger.LogError(exception, "Report {ReportId} notification {DeliveryId} failed", reportId, claimed.Id.Key);
                await SaveOutcome(claimed, "failed", cancellationToken);
                continue;
            }

            await SaveOutcome(claimed, "sent", cancellationToken);
        }

        return Status(await reports.Deliveries(reportId, cancellationToken));
    }

    private async Task<ReportEmailDelivery?> Claim(Guid deliveryId, Guid reportId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var delivery = (await reports.Deliveries(reportId, cancellationToken))
            .SingleOrDefault(item => item.Id.Key == deliveryId);
        if (delivery is null || delivery.Status is not ("pending" or "failed"))
        {
            return null;
        }

        var claimed = delivery with { Id = new(deliveryId, delivery.Id.Version + 1), Status = "sending" };
        reports.StageDelivery(claimed);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    private async Task SaveOutcome(ReportEmailDelivery delivery, string status, CancellationToken cancellationToken)
    {
        reports.StageDelivery(delivery with { Id = new(delivery.Id.Key, delivery.Id.Version + 1), Status = status });
        await context.SaveChangesAsync(cancellationToken);
    }
}
