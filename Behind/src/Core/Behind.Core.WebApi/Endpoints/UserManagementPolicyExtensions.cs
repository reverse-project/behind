using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public sealed record UserManagementActor(Guid UserId, bool IsDeveloper, bool CanDeleteUsers, UserAccess Access);

public static class UserManagementPolicyExtensions
{
    extension(IUserManagementQueries.ManagedUser user)
    {
        public bool IsAdministrator => user.Roles.Contains(DomainConstants.AdministratorsRole);

        public bool IsDeveloper => user.Roles.Contains(DomainConstants.DevelopersRole);

        public bool IsActive => user.State == DeletionState.Active;

        public bool Has(ManagedRole role) => user.Roles.Contains(role.Role.Role.Name);
    }

    extension(UserManagementActor actor)
    {
        public bool CanModerate(IUserManagementQueries.ManagedUser target) =>
            target.IsActive && actor.UserId != target.Id && (actor.IsDeveloper || !target.IsDeveloper);

        public bool CanChange(ManagedRole role, IUserManagementQueries.ManagedUser target) =>
            actor.CanModerate(target) && (actor.IsDeveloper || role != ManagedRole.Developer);

        public bool CanDelete(IUserManagementQueries.ManagedUser target) =>
            actor.CanDeleteUsers && actor.CanModerate(target);

        public bool CanManageDeleted(IUserManagementQueries.ManagedUser target) =>
            actor.IsDeveloper && !target.IsActive;
    }
}
