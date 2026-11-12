using Behind.Common;
using Behind.Common.Database.Modules;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public sealed class UserProfiles(AbstractCoreContext context, IModuleFiles files) : IModuleUsers
{
    public const string FileType = "Users";

    public async Task<IReadOnlyList<IModuleUsers.Profile>> Find(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var users = await context.Users.Where(user => ids.Contains(EF.Property<Guid>(user, "Id")))
            .Select(user => new { Id = EF.Property<Guid>(user, "Id"), user.DisplayName, user.ProfilePictureId })
            .AsNoTracking().ToListAsync(cancellationToken);

        return users.Select(user => new IModuleUsers.Profile(
            user.Id, user.Id.ToUserLabel(user.DisplayName), user.ProfilePictureId)).ToList();
    }

    public sealed record Details(DateOnly? Birthday, bool HideEmail);

    public sealed record ManagementProfile(string DisplayName, DateOnly? Birthday, bool HideEmail, DateTimeOffset? UpdatedAt);

    public sealed record PublicProfile(
        Guid Id, string DisplayName, Guid? PictureId, string? PrimaryEmail,
        DateTimeOffset? RegisteredAt, DateTimeOffset? LastLoginAt, IReadOnlyList<string> Roles);

    public sealed record BanRecord(
        Guid Id, string Reason, DateTimeOffset? ExpiresAt, Guid GivenById, string GivenBy,
        DateTimeOffset? CreatedAt, DateTimeOffset? RemovedAt, bool Removed, bool Active);

    public async Task<IReadOnlyList<BanRecord>> GetBans(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<BanRow> rows;
        using (context.SoftDelete.IncludeDeleted())
        {
            rows = await context.WallOfShameEntries.ForUser(userId)
                .Select(entry => new BanRow(
                    EF.Property<Guid>(entry, "Id"), entry.Reason.Value,
                    EF.Property<DateTimeOffset?>(entry, "ExpiresAt"),
                    EF.Property<Guid>(entry, "GivenById")))
                .AsNoTracking().ToListAsync(cancellationToken);
        }

        var keys = rows.Select(row => row.Id.ToString().ToLowerInvariant()).ToList();
        var audit = await context.AuditLogEntries
            .Where(entry => entry.EntityName == nameof(WallOfShameEntry) && keys.Contains(entry.EntityKey))
            .Select(entry => new { entry.EntityKey, entry.Action, entry.Timestamp })
            .AsNoTracking().ToListAsync(cancellationToken);
        var actors = (await Find(rows.Select(row => row.GivenById).Distinct().ToList(), cancellationToken))
            .ToDictionary(actor => actor.Id, actor => actor.DisplayName);

        return rows.Select(row =>
        {
            var events = audit.Where(entry => entry.EntityKey == row.Id.ToString().ToLowerInvariant()).ToList();
            var removed = events.Count(entry => entry.Action is AuditLogEntry.Deleted)
                          > events.Count(entry => entry.Action is AuditLogEntry.Restored);
            return new BanRecord(row.Id, row.Reason, row.ExpiresAt, row.GivenById,
                actors.GetValueOrDefault(row.GivenById, row.GivenById.ToUserLabel(null)),
                events.Where(entry => entry.Action is AuditLogEntry.Created)
                    .Select(entry => (DateTimeOffset?)entry.Timestamp).Min(),
                removed ? events.Where(entry => entry.Action is AuditLogEntry.Deleted)
                    .Select(entry => (DateTimeOffset?)entry.Timestamp).Max() : null, removed,
                !removed && (row.ExpiresAt is null || row.ExpiresAt > now));
        }).OrderByDescending(ban => ban.CreatedAt).ThenByDescending(ban => ban.Id).ToList();
    }

    private sealed record BanRow(Guid Id, string Reason, DateTimeOffset? ExpiresAt, Guid GivenById);

    public async Task<Details?> GetDetails(Guid userId, CancellationToken cancellationToken) =>
        await context.Users.Where(user => EF.Property<Guid>(user, "Id") == userId)
            .Select(user => new Details(user.Birthday, user.HideEmail))
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken);

    public async Task<ManagementProfile?> GetManagementProfile(Guid userId, CancellationToken cancellationToken)
    {
        var user = await context.Users.Where(user => EF.Property<Guid>(user, "Id") == userId)
            .Select(user => new { user.DisplayName, user.Birthday, user.HideEmail })
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var userKey = userId.ToString().ToLowerInvariant();
        var changes = await context.AuditLogEntries
            .Where(entry => entry.EntityName == nameof(User) && entry.EntityKey == userKey)
            .Select(entry => new { entry.Action, entry.Timestamp })
            .AsNoTracking().ToListAsync(cancellationToken);
        var updatedAt = changes.Where(entry => entry.Action is AuditLogEntry.Updated)
            .Select(entry => (DateTimeOffset?)entry.Timestamp).Max();

        return new ManagementProfile(user.DisplayName, user.Birthday, user.HideEmail, updatedAt);
    }

    public async Task<PublicProfile?> GetPublic(Guid userId, CancellationToken cancellationToken)
    {
        var user = await context.Users.Where(user => EF.Property<Guid>(user, "Id") == userId)
            .Select(user => new
            {
                user.DisplayName,
                user.ProfilePictureId,
                PrimaryEmail = user.HideEmail ? null : context.Emails
                    .Where(email => EF.Property<Guid>(email, "UserId") == userId)
                    .OrderBy(email => EF.Property<Guid>(email, "Id"))
                    .Select(email => email.Value)
                    .FirstOrDefault()
            }).AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var userKey = userId.ToString().ToLowerInvariant();
        var registeredAt = await context.AuditLogEntries
            .Where(entry => entry.EntityName == nameof(User) && entry.EntityKey == userKey
                && entry.Action == new AuditLogEntry.Created())
            .Select(entry => (DateTimeOffset?)entry.Timestamp)
            .MaxAsync(cancellationToken);
        var lastLoginAt = await context.Sessions
            .Where(session => EF.Property<Guid>(session, "UserId") == userId
                && EF.Property<Guid?>(session, "ImpersonatorId") == null)
            .Select(session => (DateTimeOffset?)session.RefreshToken.IssuedAt)
            .MaxAsync(cancellationToken);
        var roles = await (
                from userRole in context.Set<Dictionary<string, object>>("UserRoles")
                where EF.Property<Guid>(userRole, "UserId") == userId
                join role in context.Roles
                    on EF.Property<Guid>(userRole, "RoleId") equals EF.Property<Guid>(role, "Id")
                orderby role.Name
                select role.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PublicProfile(userId, userId.ToUserLabel(user.DisplayName), user.ProfilePictureId,
            user.PrimaryEmail, registeredAt, lastLoginAt, roles);
    }

    public async Task<bool> SetDetails(Guid userId, Details details, CancellationToken cancellationToken)
    {
        var repository = context.GetRepository<HumanUser, Guid>();
        var found = await repository.TryFindAsync(userId, cancellationToken);

        return await found.Match(
            () => Task.FromResult(false),
            async user =>
            {
                repository.Update(userId, user with { Birthday = details.Birthday, HideEmail = details.HideEmail });
                await context.SaveChangesAsync(cancellationToken);
                return true;
            });
    }

    public async Task<bool> SetPicture(Guid userId, Guid? pictureId, CancellationToken cancellationToken)
    {
        var repository = context.GetRepository<HumanUser, Guid>();
        var found = await repository.TryFindAsync(userId, cancellationToken);

        return await found.Match(
            () => Task.FromResult(false),
            async user =>
            {
                if (pictureId is { } picture)
                {
                    await files.Attach(FileType, userId, [picture], cancellationToken);
                }

                repository.Update(userId, user with { ProfilePictureId = pictureId });
                await context.SaveChangesAsync(cancellationToken);

                return true;
            });
    }

    public Task<bool> IsCurrentPicture(Guid pictureId, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(user => user.ProfilePictureId == pictureId, cancellationToken);
}
