using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class AuditLabelExtensions
{
    private const string SessionKind = "Session";
    private const string PasswordKind = "Password";
    private const string MagicLinkKind = "Magic link";
    private const string WallOfShameKind = "Wall of shame entry";
    private const string GuestLabel = "Guest";

    private sealed record Named(Guid Id, string Label);

    private sealed record Owned(Guid Id, string Kind, Guid UserId);

    private static IEnumerable<Named> BuiltInUsers =>
    [
        new(DomainConstants.SystemUserId, DomainConstants.System),
        new(DomainConstants.GuestUserId, GuestLabel)
    ];

    extension(AbstractCoreContext context)
    {
        public async Task<AuditLabels> ResolveAuditLabelsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default
        )
        {
            var wanted = ids.Distinct().ToList();
            var owned = await context.OwnedAsync(wanted, cancellationToken);
            var userIds = wanted.Concat(owned.Select(item => item.UserId)).Distinct().ToList();
            var users = (await context.UsersAsync(userIds, cancellationToken))
                .Concat(BuiltInUsers)
                .DistinctBy(item => item.Id)
                .ToDictionary(item => item.Id, item => item.Label);
            var named = await context.NamedAsync(wanted, cancellationToken);

            var ownedLabels = owned.Select(item => new Named(
                item.Id,
                $"{item.Kind} of {users.GetValueOrDefault(item.UserId, item.UserId.ToString())}"));

            return new AuditLabels(
                users.Select(pair => new Named(pair.Key, pair.Value))
                    .Concat(named)
                    .Concat(ownedLabels)
                    .DistinctBy(item => item.Id)
                    .ToDictionary(item => item.Id, item => item.Label));
        }

        private async Task<IReadOnlyList<Named>> UsersAsync(List<Guid> ids, CancellationToken cancellationToken) =>
            await context.Users
                .Where(user => ids.Contains(EF.Property<Guid>(user, "Id")))
                .Select(user => new Named(EF.Property<Guid>(user, "Id"), user.DisplayName))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        private async Task<IReadOnlyList<Named>> NamedAsync(List<Guid> ids, CancellationToken cancellationToken) =>
        [
            .. await context.Roles
                .Where(role => ids.Contains(EF.Property<Guid>(role, "Id")))
                .Select(role => new Named(EF.Property<Guid>(role, "Id"), role.Name))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Grants
                .Where(grant => ids.Contains(EF.Property<Guid>(grant, "Id")))
                .Select(grant => new Named(EF.Property<Guid>(grant, "Id"), grant.Name))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Restrictions
                .Where(restriction => ids.Contains(EF.Property<Guid>(restriction, "Id")))
                .Select(restriction => new Named(EF.Property<Guid>(restriction, "Id"), restriction.Name))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Emails
                .Where(email => ids.Contains(EF.Property<Guid>(email, "Id")))
                .Select(email => new Named(EF.Property<Guid>(email, "Id"), email.Value))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.UserAgents
                .Where(agent => ids.Contains(EF.Property<Guid>(agent, "Id")))
                .Select(agent => new Named(EF.Property<Guid>(agent, "Id"), agent.Value))
                .AsNoTracking()
                .ToListAsync(cancellationToken)
        ];

        private async Task<IReadOnlyList<Owned>> OwnedAsync(List<Guid> ids, CancellationToken cancellationToken) =>
        [
            .. await context.Sessions
                .Where(session => ids.Contains(EF.Property<Guid>(session, "Id")))
                .Select(session => new Owned(
                    EF.Property<Guid>(session, "Id"),
                    SessionKind,
                    EF.Property<Guid>(session, "UserId")))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.Passwords
                .Where(password => ids.Contains(EF.Property<Guid>(password, "Id")))
                .Select(password => new Owned(
                    EF.Property<Guid>(password, "Id"),
                    PasswordKind,
                    EF.Property<Guid>(password, "UserId")))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.MagicLinks
                .Where(link => ids.Contains(EF.Property<Guid>(link, "Id")))
                .Select(link => new Owned(
                    EF.Property<Guid>(link, "Id"),
                    MagicLinkKind,
                    EF.Property<Guid>(link, "UserId")))
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            .. await context.WallOfShameEntries
                .Where(entry => ids.Contains(EF.Property<Guid>(entry, "Id")))
                .Select(entry => new Owned(
                    EF.Property<Guid>(entry, "Id"),
                    WallOfShameKind,
                    EF.Property<Guid>(entry, "UserId")))
                .AsNoTracking()
                .ToListAsync(cancellationToken)
        ];
    }
}
