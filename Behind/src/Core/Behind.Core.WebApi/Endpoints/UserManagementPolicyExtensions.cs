using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed record UserManagementActor(
    Guid UserId,
    bool IsDeveloper,
    bool CanDeleteUsers,
    UserAccess Access,
    AccessCatalog Catalog,
    bool IsImpersonating = false);

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

        public bool CanImpersonate(IUserManagementQueries.ManagedUser target) =>
            !actor.IsImpersonating
            && actor.Access.Holds(GrantName.Of(GrantArea.Impersonation, GrantAction.Manage))
            && target.Ban == None
            && actor.CanModerate(target);

        public bool CanConfirmEmail(IUserManagementQueries.ManagedUser target) =>
            !target.EmailConfirmed && target.Emails.Count > 0 && actor.CanModerate(target);

        public bool CanManageDeleted(IUserManagementQueries.ManagedUser target) =>
            actor.IsDeveloper && !target.IsActive;
    }
}
