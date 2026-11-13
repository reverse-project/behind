using Behind.Common;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Core.WebApi.Endpoints;

public static class AuditLogEntryEndpoints
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 200;
    public const int DefaultRangeDays = 7;
    public const string NewestFirst = "audit.Timestamp DESC";
    public const string OldestFirst = "audit.Timestamp ASC";
    private static readonly string[] SortFields = ["timestamp", "entity", "key", "action", "actor"];
    private static readonly string[] Actions =
    [
        "Created", "Updated", "Deleted", "HardDeleted", "Restored", "ChangeOwnership", "Moved",
        "ImpersonationStarted", "ImpersonationEnded"
    ];

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> GetEntityTypes(HttpContext httpContext, IAuditLogEntryQueries queries)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        return Results.Json(await queries.GetEntityTypes(httpContext.RequestAborted));
    }

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> Get(
        HttpContext httpContext,
        IAuditLogEntryQueries queries,
        TimeProvider timeProvider,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] string? search = null,
        [FromQuery] bool oldestFirst = false,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? entity = null,
        [FromQuery] string? action = null,
        [FromQuery] string? actor = null,
        [FromQuery] string? behalfOf = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool descending = false,
        [FromQuery] bool entityExact = false)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var to = toDate ?? today;
        var from = fromDate ?? (to.DayNumber >= DefaultRangeDays ? to.AddDays(-DefaultRangeDays) : DateOnly.MinValue);

        if (from > to || to == DateOnly.MaxValue
            || (sortBy is not null && !SortFields.Contains(sortBy))
            || (!string.IsNullOrEmpty(action) && !Actions.Contains(action))
            || new[] { entity, actor, behalfOf }.Any(value => value?.Length > MaxSearchLength))
        {
            return Results.BadRequest();
        }

        var flipper = Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize);
        if ((long)flipper.Index * flipper.Size > int.MaxValue)
        {
            return Results.BadRequest();
        }
        var searchTerm = search.ToSearchTerm(MaxSearchLength);
        entity = entity?.Trim();
        actor = actor?.Trim();
        behalfOf = behalfOf?.Trim();

        if (searchTerm.Match(() => false, _ => true)
            || !string.IsNullOrEmpty(entity) || !string.IsNullOrEmpty(action)
            || !string.IsNullOrEmpty(actor) || !string.IsNullOrEmpty(behalfOf)
            || (sortBy is not null && sortBy != "timestamp"))
        {
            return Results.Json(await Search(
                queries, from, to, searchTerm.Match(() => "", term => term),
                entity, entityExact, action, actor, behalfOf, sortBy ?? "timestamp",
                sortBy is null ? !oldestFirst : descending, flipper, httpContext.RequestAborted));
        }

        var entries = await queries.GetAuditLog(
            IAuditLogEntryQueries.RequestAuditLog.Create(
                from,
                to,
                searchTerm.ToAuditLogFilter(),
                flipper,
                (sortBy is null ? oldestFirst : !descending) ? OldestFirst : NewestFirst),
            httpContext.RequestAborted);

        var items = entries.Match(
            () => (IReadOnlyList<AuditLogEntry>)[],
            paged => paged.Results.Match(() => (IReadOnlyList<AuditLogEntry>)[], found => found.ToList()));

        var labels = await queries.ResolveLabels(items.References, httpContext.RequestAborted);

        return Results.Json(entries.Match(
            () => new PageResponse<AuditLogItem>(0, flipper.Index, 0, flipper.Size, []),
            paged => (paged with { Results = Some<IEnumerable<AuditLogEntry>>(items) })
                .ToPageResponse(entry => entry.ToItem(labels))));
    }

    private static async Task<PageResponse<AuditLogItem>> Search(
        IAuditLogEntryQueries queries, DateOnly from, DateOnly to, string search, string? entity, bool entityExact,
        string? action, string? actor, string? behalfOf, string sortBy, bool descending,
        Flipper flipper, CancellationToken cancellationToken)
    {
        var skip = ((long)flipper.Index - 1) * flipper.Size;
        var keep = (int)Math.Min(skip + flipper.Size, int.MaxValue);
        var comparer = Comparer<Candidate>.Create((left, right) =>
        {
            var comparison = sortBy switch
            {
                "entity" => StringComparer.OrdinalIgnoreCase.Compare(left.Item.EntityName, right.Item.EntityName),
                "key" => StringComparer.OrdinalIgnoreCase.Compare(left.Item.EntityKey, right.Item.EntityKey),
                "action" => StringComparer.OrdinalIgnoreCase.Compare(left.Item.Action, right.Item.Action),
                "actor" => StringComparer.OrdinalIgnoreCase.Compare(left.Item.Actor.Label, right.Item.Actor.Label),
                _ => left.Item.Timestamp.CompareTo(right.Item.Timestamp)
            };
            if (comparison != 0)
            {
                return descending ? -comparison : comparison;
            }
            return left.Position.CompareTo(right.Position);
        });
        var matches = new PriorityQueue<Candidate, Candidate>(
            Comparer<Candidate>.Create((left, right) => comparer.Compare(right, left)));
        long total = 0;
        var batch = 1;
        long pageCount;
        do
        {
            var request = IAuditLogEntryQueries.RequestAuditLog.Create(
                from, to, new EmptyFilter<AuditLogEntry>(), new Flipper(batch, MaxPageSize), NewestFirst);
            var result = await queries.GetAuditLog(request, cancellationToken);
            var entries = result.Match(
                () => (IReadOnlyList<AuditLogEntry>)[],
                page => page.Results.Match(() => (IReadOnlyList<AuditLogEntry>)[], items => items.ToList()));
            pageCount = result.Match(() => 0, page => page.PageCount);
            var candidates = entries.Where(entry =>
            {
                var entryActor = entry.User.ToActor(entry.ActorId);
                var entryBehalfOf = entry.BehalfOf.Match<AuditLogActor?>(() => null, user => user.ToActor(entry.BehalfOfActorId));
                return (string.IsNullOrEmpty(entity) || (entityExact
                        ? string.Equals(entry.EntityName, entity, StringComparison.Ordinal)
                        : Contains(entry.EntityName, entity)))
                    && (string.IsNullOrEmpty(action) || entry.Action.GetType().Name == action)
                    && MatchesActor(entryActor, actor)
                    && (string.IsNullOrEmpty(behalfOf) || entryBehalfOf is not null && MatchesActor(entryBehalfOf, behalfOf));
            }).ToList();
            var labels = await queries.ResolveLabels(candidates.References, cancellationToken);

            foreach (var entry in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = entry.ToItem(labels);
                var rawChanges = entry.Action.ToChanges();
                if (string.IsNullOrEmpty(search)
                        || Contains(item.EntityName, search) || Contains(item.EntityKey, search)
                        || Contains(item.EntityLabel, search) || MatchesActor(item.Actor, search)
                        || item.OnBehalfOf is not null && MatchesActor(item.OnBehalfOf, search)
                        || item.Changes.Concat(rawChanges).Any(change =>
                            Contains(change.Property, search) || Contains(change.OldValue, search)
                            || Contains(change.NewValue, search)))
                {
                    var candidate = new Candidate(item, total++);
                    if (keep > 0)
                    {
                        matches.Enqueue(candidate, candidate);
                        if (matches.Count > keep)
                        {
                            matches.Dequeue();
                        }
                    }
                }
            }

            batch++;
        } while (batch <= pageCount);

        var itemsOnPage = matches.UnorderedItems.Select(pair => pair.Element).OrderBy(item => item, comparer)
            .Skip((int)skip).Take(flipper.Size).Select(item => item.Item).ToList();
        return new PageResponse<AuditLogItem>(
            total, flipper.Index, flipper.PageCount(total), flipper.Size, itemsOnPage);
    }

    private sealed record Candidate(AuditLogItem Item, long Position);

    private static bool Contains(string? value, string? term) =>
        string.IsNullOrEmpty(term) || value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    private static bool MatchesActor(AuditLogActor actor, string? term) =>
        Contains(actor.Label, term) || actor.Emails.Any(email => Contains(email, term));
}
