using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Queries;

public sealed class ScheduleParticipantQueries(AbstractCoreContext context) : IScheduleParticipantQueries
{
    private const string IdColumn = "Id";
    private const string UserIdColumn = "UserId";
    private const string RoleIdColumn = "RoleId";
    private const string UserRolesEntity = "UserRoles";

    public async Task<IScheduleParticipantQueries.ParticipantDirectory> Directory(
        string? search,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var term = (search ?? string.Empty).Trim().ToLowerInvariant();
        var roles = await ActiveRoles()
            .Where(role => term.Length == 0 || role.Name.ToLower().Contains(term))
            .OrderBy(role => role.Name)
            .Select(role => new IScheduleParticipantQueries.ParticipantRole(EF.Property<Guid>(role, IdColumn), role.Name))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var users = await ActiveUsers()
            .Where(user => term.Length == 0 || user.DisplayName.ToLower().Contains(term))
            .OrderBy(user => user.DisplayName)
            .Select(user => new { Id = EF.Property<Guid>(user, IdColumn), user.DisplayName })
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new IScheduleParticipantQueries.ParticipantDirectory(
            roles,
            users.Select(user => new IScheduleParticipantQueries.ParticipantUser(user.Id, user.Id.ToUserLabel(user.DisplayName)))
                .ToList());
    }

    public async Task<IReadOnlyList<IScheduleParticipantQueries.LabeledParticipant>> Label(
        ScheduleParticipants participants,
        CancellationToken cancellationToken = default)
    {
        var roleIds = participants.RoleIds.ToArray();
        var userIds = participants.UserIds.ToArray();
        var roles = await context.Roles
            .Where(role => roleIds.Contains(EF.Property<Guid>(role, IdColumn)))
            .Select(role => new { Id = EF.Property<Guid>(role, IdColumn), role.Name })
            .AsNoTracking()
            .ToDictionaryAsync(role => role.Id, role => role.Name, cancellationToken);
        var users = await context.Users
            .Where(user => userIds.Contains(EF.Property<Guid>(user, IdColumn)))
            .Select(user => new { Id = EF.Property<Guid>(user, IdColumn), user.DisplayName })
            .AsNoTracking()
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);

        return participants.Items
            .Select(participant => new IScheduleParticipantQueries.LabeledParticipant(
                participant.Kind,
                participant.Value,
                participant.Kind switch
                {
                    ScheduleParticipantKind.Role => participant.Identifier
                        .Bind(id => roles.TryGetValue(id, out var name) ? FunctionalExtensions.Some(name) : FunctionalExtensions.None)
                        .GetOrElse(participant.Value),
                    ScheduleParticipantKind.User => participant.Identifier
                        .Map(id => id.ToUserLabel(users.GetValueOrDefault(id)))
                        .GetOrElse(participant.Value),
                    _ => participant.Value
                }))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> Emails(
        ScheduleParticipants participants,
        CancellationToken cancellationToken = default)
    {
        var requestedRoles = participants.RoleIds.ToArray();
        var roleIds = await ActiveRoles()
            .Select(role => EF.Property<Guid>(role, IdColumn))
            .Where(id => requestedRoles.Contains(id))
            .ToArrayAsync(cancellationToken);
        var members = await context.Set<Dictionary<string, object>>(UserRolesEntity)
            .Where(row => roleIds.Contains(EF.Property<Guid>(row, RoleIdColumn)))
            .Select(row => EF.Property<Guid>(row, UserIdColumn))
            .ToArrayAsync(cancellationToken);
        var audience = participants.UserIds.Concat(members)
            .Where(id => id != DomainConstants.SystemUserId && id != DomainConstants.GuestUserId)
            .Distinct()
            .ToArray();
        var activeUsers = ActiveUsers().Select(user => EF.Property<Guid>(user, IdColumn));
        var addresses = await context.Emails
            .Where(email => email.ConfirmedAt != null
                            && audience.Contains(EF.Property<Guid>(email, UserIdColumn))
                            && activeUsers.Contains(EF.Property<Guid>(email, UserIdColumn)))
            .Select(email => email.Value)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return addresses
            .Concat(participants.Emails)
            .Select(address => address.Trim())
            .Where(address => address.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IQueryable<UserRole> ActiveRoles() =>
        context.Roles.InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active);

    private IQueryable<HumanUser> ActiveUsers() =>
        context.Users
            .InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active)
            .Where(user => EF.Property<Guid>(user, IdColumn) != DomainConstants.SystemUserId
                           && EF.Property<Guid>(user, IdColumn) != DomainConstants.GuestUserId);
}
