using System.ComponentModel.DataAnnotations;
using System.Data;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Common.Configuration;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public static class ReportEndpoints
{
    public sealed record CreateRequest(
        [property: MaxLength(UserReport.MaxTargets)] Guid[]? UserIds,
        [property: Required, MaxLength(UserReport.MaxReasonLength)] string Reason,
        [property: MaxLength(40)] string? SourceType,
        Guid? SourceId);

    public sealed record ResolveRequest(
        [property: Required, MaxLength(16)] string Action,
        DateTimeOffset? BanUntil,
        [property: MaxLength(UserReport.MaxReasonLength)] string? ReporterMessage,
        [property: MaxLength(UserReport.MaxReasonLength)] string? ReportedEmail,
        long? Version = null);

    [IgnoreETag]
    public static async Task<IResult> Create(
        CreateRequest request, HttpContext http, IModuleIdentity identity,
        IEnumerable<IReportEvidenceSource> sources, CoreReportEvidenceQueries coreEvidence,
        IAuthorizationService authorization, AbstractCoreContext context, ReportStore reports,
        IReportConversations conversations, TimeProvider clock, IOptionsMonitor<CommentsConfiguration> comments)
    {
        http.Response.Headers.CacheControl = "no-store";
        var reporter = await identity.UserId(http);
        if (reporter is null)
        {
            return Results.Unauthorized();
        }

        var reason = request.Reason?.Trim() ?? "";
        if (reason.Length is 0 or > UserReport.MaxReasonLength
            || request.UserIds?.Length > UserReport.MaxTargets
            || (request.SourceType is null) != (request.SourceId is null))
        {
            return Error("invalidReport");
        }

        ReportEvidence? evidence = null;
        if (request.SourceType is { } sourceType && request.SourceId is { } sourceId)
        {
            var adapter = sources.SingleOrDefault(source => source.SourceType == sourceType);
            if (adapter is not null)
            {
                evidence = await adapter.Find(http, sourceId, http.RequestAborted);
            }
            else if (sourceType is "profile" or "chatMessage" or "newsComment")
            {
                if (sourceType == "newsComment" && !comments.CurrentValue.News)
                {
                    return Results.NotFound();
                }

                if (sourceType == "chatMessage"
                    && !await authorization.HoldsGrantAsync(http.User, ChatEndpoints.ReadGrant))
                {
                    return Results.NotFound();
                }

                var found = await coreEvidence.Find(sourceType, sourceId, clock.GetUtcNow(), http.RequestAborted);
                evidence = found is null ? null : new(sourceType, sourceId, found.AuthorId, found.Content);
            }

            if (evidence is null)
            {
                return Results.NotFound();
            }
        }

        var targets = (request.UserIds ?? []).Concat(evidence is null ? [] : new[] { evidence.AuthorId })
            .Distinct().ToArray();
        if (targets.Length is 0 or > UserReport.MaxTargets
            || targets.Contains(reporter.Value) || targets.Contains(Guid.Empty)
            || targets.Contains(DomainConstants.SystemUserId) || targets.Contains(DomainConstants.GuestUserId)
            || evidence?.Content.Length > UserReport.MaxEvidenceLength)
        {
            return Error("invalidReportTargets");
        }

        var activeTargets = await context.Users.Where(user => targets.Contains(EF.Property<Guid>(user, "Id")))
            .CountAsync(http.RequestAborted);
        if (activeTargets != targets.Length)
        {
            return Error("invalidReportTargets");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, http.RequestAborted);
        var conversationId = await conversations.CreateReport(reporter.Value, "User report", reason, http.RequestAborted);
        var id = Guid.NewGuid();
        await reports.Save(new UserReport(new(id, 1), reporter.Value, conversationId,
            string.Join(',', targets), reason, evidence?.SourceType ?? "", evidence?.SourceId ?? Guid.Empty,
            evidence?.AuthorId, evidence?.Content ?? "", "open", null, "", "", ""), http.RequestAborted);
        await transaction.CommitAsync(http.RequestAborted);
        return Results.Created($"/reports/{id}", new { id, conversationId });
    }

    [IgnoreETag]
    public static async Task<IResult> List(
        HttpContext http, IModuleIdentity identity, ReportStore reports, IModuleUsers users,
        IAuthorizationService authorization, AbstractCoreContext context, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, [FromQuery] bool deleted = false)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (await identity.UserId(http) is null)
        {
            return Results.Unauthorized();
        }

        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100
            || status is not (null or "open" or "resolved" or "dismissed"))
        {
            return Error("invalidReportQuery");
        }

        if (deleted && !await authorization.HoldsGrantAsync(http.User, ManagedListExtensions.DeletedRecordsGrant))
        {
            return Results.Forbid();
        }

        var result = await reports.List(status, page, pageSize, deleted, http.RequestAborted);
        var profiles = (await users.Find(result.Items.Select(item => item.ReporterId).Distinct().ToArray(),
            http.RequestAborted)).ToDictionary(user => user.Id, user => user.DisplayName);
        var times = await context.AuditTimesAsync<UserReport>(
            result.Items.ToDictionary(report => report.Id.Key, report => report.Id.Version), http.RequestAborted);
        var items = result.Items.Select(report => new
        {
            id = report.Id.Key, version = report.Id.Version, report.Status, report.Reason,
            report.ConversationId, report.ReporterId,
            reporter = profiles.GetValueOrDefault(report.ReporterId, report.ReporterId.ToUserLabel(null)),
            subject = "User report",
            createdAt = times[report.Id.Key].CreatedAt.Match<DateTimeOffset?>(() => null, value => value),
            updatedAt = times[report.Id.Key].UpdatedAt.Match<DateTimeOffset?>(() => null, value => value)
        });
        return Results.Ok(new { result.Total, page, pageSize, result.PageCount, items });
    }

    [IgnoreETag]
    public static async Task<IResult> Get(
        Guid id, HttpContext http, IModuleIdentity identity, ReportStore reports, IModuleUsers users,
        IAuthorizationService authorization, AbstractCoreContext context, [FromQuery] bool deleted = false)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (await identity.UserId(http) is null)
        {
            return Results.Unauthorized();
        }

        if (deleted && !await authorization.HoldsGrantAsync(http.User, ManagedListExtensions.DeletedRecordsGrant))
        {
            return Results.Forbid();
        }

        var report = await reports.Find(id, deleted, http.RequestAborted);
        if (report is null)
        {
            return Results.NotFound();
        }

        var profileIds = report.Users.Append(report.ReporterId);
        if (report.ResolvedById is { } moderator)
        {
            profileIds = profileIds.Append(moderator);
        }

        var profiles = (await users.Find(profileIds.Distinct().ToArray(), http.RequestAborted))
            .ToDictionary(user => user.Id, user => user.DisplayName);
        var deliveries = await reports.Deliveries(id, http.RequestAborted);
        var times = (await context.AuditTimesAsync<UserReport>(
            new Dictionary<Guid, long> { [id] = report.Id.Version }, http.RequestAborted))[id];
        return Results.Ok(new
        {
            id, version = report.Id.Version, report.Status, report.Reason, report.ConversationId,
            report.ReporterId, reporter = profiles.GetValueOrDefault(report.ReporterId, report.ReporterId.ToUserLabel(null)),
            reportedUsers = report.Users.Select(userId => new
            {
                id = userId, displayName = profiles.GetValueOrDefault(userId, userId.ToUserLabel(null))
            }),
            evidence = report.SourceId == Guid.Empty ? [] : new[]
            {
                new { report.SourceType, report.SourceId, content = report.EvidenceContent, authorId = report.SourceAuthorId }
            },
            report.ResolvedById, report.Resolution, report.ReporterMessage, report.ReportedEmail,
            resolvedByName = report.ResolvedById is { } handler
                ? profiles.GetValueOrDefault(handler, handler.ToUserLabel(null)) : null,
            emailDeliveryStatus = ReportEmailDispatcher.Status(deliveries),
            createdAt = times.CreatedAt.Match<DateTimeOffset?>(() => null, value => value),
            updatedAt = times.UpdatedAt.Match<DateTimeOffset?>(() => null, value => value)
        });
    }

    [IgnoreETag]
    public static async Task<IResult> Conversation(
        Guid id, HttpContext http, IModuleIdentity identity, ReportStore reports, PrivateMessageStore messages)
    {
        http.Response.Headers.CacheControl = "no-store";
        var viewer = await identity.UserId(http);
        if (viewer is null)
        {
            return Results.Unauthorized();
        }

        var report = await reports.Find(id, false, http.RequestAborted);
        if (report is null)
        {
            return Results.NotFound();
        }

        var conversation = await messages.Find(viewer.Value, report.ConversationId,
            reportModerator: true, cancellationToken: http.RequestAborted);
        return conversation is null ? Results.NotFound() : Results.Ok(conversation);
    }

    [IgnoreETag]
    public static Task<IResult> Resolve(
        Guid id, ResolveRequest request, HttpContext http, UserManagementGate gate,
        IUserManagementQueries userQueries, IWallOfShameCommands bans, ISessionCommands sessions,
        AbstractCoreContext context, ReportStore reports, IReportConversations conversations,
        ReportEmailDispatcher dispatcher) =>
        gate.RunAs(http, async actor =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var now = dispatcher.UtcNow;
            if (request.Action is not ("dismiss" or "resolve" or "ban")
                || request.Action == "ban" && request.BanUntil <= now)
            {
                return Error("invalidReportResolution");
            }

            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, http.RequestAborted);
            var report = await reports.Find(id, false, http.RequestAborted);
            if (report is null)
            {
                return Results.NotFound();
            }

            if (report.Status != "open" || request.Version is { } version && version != report.Id.Version)
            {
                return Results.Conflict(new { error = "reportAlreadyResolved" });
            }

            if (request.Action == "ban")
            {
                foreach (var userId in report.Users)
                {
                    var target = await userQueries.TryFindUser(userId, DeletionState.Active, now, http.RequestAborted);
                    if (!target.Match(() => false, actor.CanModerate))
                    {
                        return Results.Forbid();
                    }
                }
            }

            var emailBody = request.ReportedEmail?.Trim() ?? "";
            if (emailBody.Length > 0)
            {
                foreach (var userId in report.Users)
                {
                    var email = await context.Emails
                        .Where(address => EF.Property<Guid>(address, "UserId") == userId && address.ConfirmedAt != null)
                        .OrderBy(address => address.Value).Select(address => address.Value)
                        .FirstOrDefaultAsync(http.RequestAborted);
                    if (email is null)
                    {
                        return Error("reportedUserHasNoConfirmedEmail");
                    }

                    reports.StageDelivery(new(new(Guid.NewGuid(), 1), id, userId, email, emailBody, "pending"));
                }
            }

            if (request.Action == "ban")
            {
                foreach (var userId in report.Users)
                {
                    await bans.BanUntil(userId, actor.UserId, new Reason("Misconduct"), request.BanUntil, http.RequestAborted);
                    await sessions.InvalidateAllSessions(userId, actor.UserId, now, http.RequestAborted);
                }
            }

            var reporterMessage = request.ReporterMessage?.Trim() ?? "";
            if (reporterMessage.Length > 0)
            {
                await conversations.AppendReportMessage(report.ConversationId, actor.UserId, reporterMessage, http.RequestAborted);
            }

            await reports.Save(report with
            {
                Id = new(id, report.Id.Version + 1),
                Status = request.Action == "dismiss" ? "dismissed" : "resolved",
                ResolvedById = actor.UserId,
                Resolution = request.Action,
                ReporterMessage = reporterMessage,
                ReportedEmail = emailBody
            }, http.RequestAborted);
            await transaction.CommitAsync(http.RequestAborted);
            var deliveryStatus = await dispatcher.Deliver(id, http.RequestAborted);
            return Results.Ok(new { status = request.Action == "dismiss" ? "dismissed" : "resolved", emailDeliveryStatus = deliveryStatus });
        });

    [IgnoreETag]
    public static async Task<IResult> Retry(
        Guid id, HttpContext http, IModuleIdentity identity, ReportStore reports, ReportEmailDispatcher dispatcher)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (await identity.UserId(http) is null)
        {
            return Results.Unauthorized();
        }

        if (await reports.Find(id, false, http.RequestAborted) is null)
        {
            return Results.NotFound();
        }

        var deliveries = await reports.Deliveries(id, http.RequestAborted);
        if (!deliveries.Any(delivery => delivery.Status is "pending" or "failed"))
        {
            return Results.Conflict(new { error = "emailNotRetryable" });
        }

        return Results.Ok(new { emailDeliveryStatus = await dispatcher.Deliver(id, http.RequestAborted) });
    }

    [IgnoreETag]
    public static async Task<IResult> Delete(
        Guid id, HttpContext http, IModuleIdentity identity, ReportStore reports,
        IReportConversations conversations, AbstractCoreContext context)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (await identity.UserId(http) is null)
        {
            return Results.Unauthorized();
        }

        var action = http.Request.Path.Value!.EndsWith("/hard/delete", StringComparison.Ordinal) ? "hard/delete"
            : http.Request.Path.Value.EndsWith("/restore", StringComparison.Ordinal) ? "restore" : "delete";
        var report = await reports.Find(id, action != "delete", http.RequestAborted);
        if (report is null)
        {
            return Results.NotFound();
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, http.RequestAborted);
        if (!await reports.Delete(id, action, http.RequestAborted))
        {
            return Results.NotFound();
        }

        if (action == "hard/delete")
        {
            await conversations.DeleteReportConversation(report.ConversationId, http.RequestAborted);
        }

        await transaction.CommitAsync(http.RequestAborted);
        return Results.NoContent();
    }

    private static IResult Error(string error) => Results.BadRequest(new { error });
}
