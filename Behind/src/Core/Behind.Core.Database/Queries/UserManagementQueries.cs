using Behind.Common;
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
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var matching = InBucket(Matching(search), letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(
                matching
                    .OrderBy(user => user.Label.ToLower())
                    .ThenBy(user => user.Id)
                    .Select(user => new UserRow(user.Id, user.DisplayName)))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var users = await Hydrate(rows, now, cancellationToken);

        return new PagedResult<IUserManagementQueries.ManagedUser>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            users.Count > 0 ? Some<IEnumerable<IUserManagementQueries.ManagedUser>>(users) : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        CancellationToken cancellationToken = default)
    {
        var firstCharacters = await Matching(search)
            .Select(user => user.Label.ToLower().Substring(0, 1))
            .Distinct()
            .ToListAsync(cancellationToken);

        var present = firstCharacters.Select(first => first.ToLetterBucket()).ToHashSet();

        return LetterBucketExtensions.All.Where(present.Contains).ToList();
    }

    public async Task<Option<IUserManagementQueries.ManagedUser>> TryFindUser(
        Guid id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var rows = await Labeled()
            .Where(user => user.Id == id)
            .Select(user => new UserRow(user.Id, user.DisplayName))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (await Hydrate(rows, now, cancellationToken)).Head();
    }

    private IQueryable<LabeledUser> Labeled() =>
        context.Users
            .Where(user => EF.Property<Guid>(user, "Id") != DomainConstants.SystemUserId
                           && EF.Property<Guid>(user, "Id") != DomainConstants.GuestUserId)
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

    private IQueryable<LabeledUser> Matching(Option<string> search)
    {
        var people = Labeled();

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

    private static IQueryable<LabeledUser> InBucket(IQueryable<LabeledUser> users, Option<string> letter) =>
        letter.Match(
            () => users,
            bucket => bucket == LetterBucketExtensions.Other
                ? users.Where(user => user.Label == ""
                                      || user.Label.ToLower().Substring(0, 1).CompareTo("a") < 0
                                      || user.Label.ToLower().Substring(0, 1).CompareTo("z") > 0)
                : users.Where(user => user.Label.ToLower().StartsWith(bucket.ToLowerInvariant())));

    private async Task<IReadOnlyList<IUserManagementQueries.ManagedUser>> Hydrate(
        IReadOnlyList<UserRow> rows,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ids = rows.Select(row => row.Id).ToList();

        var emails = (await context.Emails
                .Where(email => ids.Contains(EF.Property<Guid>(email, "UserId")))
                .OrderBy(email => EF.Property<Guid>(email, "Id"))
                .Select(email => new { UserId = EF.Property<Guid>(email, "UserId"), email.Value })
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .ToLookup(email => email.UserId, email => email.Value);

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
                emails[row.Id].ToList(),
                roles[row.Id].ToList(),
                bans.TryGetValue(row.Id, out var ban) ? Some(ban) : None))
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
