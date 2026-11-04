using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class UserManagementQueries(AbstractCoreContext context) : IUserManagementQueries
{
    public async Task<PagedResult<IUserManagementQueries.ManagedUser>> ListUsers(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Matching(search, state).InBucket(user => user.Label, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(
                matching
                    .OrderBy(user => user.Label.ToLower())
                    .ThenBy(user => user.Id)
                    .Select(user => new UserRow(user.Id, user.DisplayName)))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var users = await Hydrate(rows, state, now, cancellationToken);

        return new PagedResult<IUserManagementQueries.ManagedUser>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            users.Count > 0 ? Some<IEnumerable<IUserManagementQueries.ManagedUser>>(users) : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Matching(search, state).LettersAsync(user => user.Label, cancellationToken);
    }

    public async Task<Option<IUserManagementQueries.ManagedUser>> TryFindUser(
        Guid id,
        DeletionState state,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var rows = await Labeled(state)
            .Where(user => user.Id == id)
            .Select(user => new UserRow(user.Id, user.DisplayName))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (await Hydrate(rows, state, now, cancellationToken)).Head();
    }

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<LabeledUser> Labeled(DeletionState state)
    {
        var deleted = state == DeletionState.Deleted;

        return context.Users
            .Where(user => EF.Property<Guid>(user, "Id") != DomainConstants.SystemUserId
                           && EF.Property<Guid>(user, "Id") != DomainConstants.GuestUserId)
            .Where(user =>
                (context.AuditLogEntries.Count(audit =>
                     audit.EntityName == nameof(User)
                     && audit.EntityKey == EF.Property<Guid>(user, "Id").ToString().ToLower()
                     && audit.Action == new AuditLogEntry.Deleted())
                 > context.AuditLogEntries.Count(audit =>
                     audit.EntityName == nameof(User)
                     && audit.EntityKey == EF.Property<Guid>(user, "Id").ToString().ToLower()
                     && audit.Action == new AuditLogEntry.Restored())) == deleted)
            .Select(user => new LabeledUser
            {
                Id = EF.Property<Guid>(user, "Id"),
                DisplayName = user.DisplayName,
                Label = user.DisplayName.Trim() != ""
                    ? user.DisplayName.Trim()
                    : context.Emails
                        .Where(email => EF.Property<Guid>(email, "UserId") == EF.Property<Guid>(user, "Id"))
                        .OrderBy(email => EF.Property<Guid>(email, "Id"))
                        .Select(email => email.Value)
                        .FirstOrDefault() ?? ""
            });
    }

    private IQueryable<LabeledUser> Matching(Option<string> search, DeletionState state)
    {
        var people = Labeled(state);

        return search
            .Map(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .Match(
                () => people,
                term => people.Where(user =>
                    user.DisplayName.ToLower().Contains(term)
                    || context.Emails.Any(email =>
                        EF.Property<Guid>(email, "UserId") == user.Id
                        && email.Value.Contains(term))));
    }

    private async Task<IReadOnlyList<IUserManagementQueries.ManagedUser>> Hydrate(
        IReadOnlyList<UserRow> rows,
        DeletionState state,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ids = rows.Select(row => row.Id).ToList();

        var emails = (await context.Emails
                .Where(email => ids.Contains(EF.Property<Guid>(email, "UserId")))
                .OrderBy(email => EF.Property<Guid>(email, "Id"))
                .Select(email => new
                {
                    UserId = EF.Property<Guid>(email, "UserId"),
                    email.Value,
                    Confirmed = email.ConfirmedAt != null
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .ToLookup(email => email.UserId);

        var roles = (await (
                    from userRole in context.Set<Dictionary<string, object>>("UserRoles")
                    where ids.Contains(EF.Property<Guid>(userRole, "UserId"))
                    join role in context.Roles
                        on EF.Property<Guid>(userRole, "RoleId") equals EF.Property<Guid>(role, "Id")
                    orderby role.Name
                    select new { UserId = EF.Property<Guid>(userRole, "UserId"), role.Name })
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .ToLookup(role => role.UserId, role => role.Name);

        var bans = (await context.WallOfShameEntries
                .ActiveAt(now)
                .Where(entry => ids.Contains(EF.Property<Guid>(entry, "UserId")))
                .OrderByDescending(entry => EF.Property<Guid>(entry, "Id"))
                .Select(entry => new
                {
                    UserId = EF.Property<Guid>(entry, "UserId"),
                    entry.Reason,
                    Expires = EF.Property<DateTimeOffset?>(entry, "ExpiresAt")
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .GroupBy(entry => entry.UserId)
            .ToDictionary(
                group => group.Key,
                group => new IWallOfShameQueries.ActiveBan(group.First().Reason.Value, group.First().Expires.ToOption()));

        return rows
            .Select(row => new IUserManagementQueries.ManagedUser(
                row.Id,
                row.DisplayName,
                emails[row.Id].Select(email => email.Value).ToList(),
                roles[row.Id].ToList(),
                bans.TryGetValue(row.Id, out var ban) ? Some(ban) : None,
                state,
                emails[row.Id].Select(email => email.Confirmed).DefaultIfEmpty(true).First()))
            .ToList();
    }

    private sealed record UserRow(Guid Id, string DisplayName);

    private sealed record LabeledUser
    {
        public Guid Id { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public string Label { get; init; } = string.Empty;
    }
}
