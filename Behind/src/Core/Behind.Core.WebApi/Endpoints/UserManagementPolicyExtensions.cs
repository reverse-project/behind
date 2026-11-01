using Behind.Core.Database;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public sealed record UserManagementActor(Guid UserId, bool IsDeveloper);

public static class UserManagementPolicyExtensions
{
    extension(IUserManagementQueries.ManagedUser user)
    {
        public bool IsAdministrator => user.Roles.Contains(DomainConstants.AdministratorsRole);

        public bool IsDeveloper => user.Roles.Contains(DomainConstants.DevelopersRole);

        public bool Has(ManagedRole role) => user.Roles.Contains(role.Role.Role.Name);
    }

    extension(UserManagementActor actor)
    {
        public bool CanModerate(IUserManagementQueries.ManagedUser target) =>
            actor.UserId != target.Id && (actor.IsDeveloper || !target.IsDeveloper);

        public bool CanChange(ManagedRole role, IUserManagementQueries.ManagedUser target) =>
            actor.CanModerate(target) && (actor.IsDeveloper || role != ManagedRole.Developer);
    }
}
