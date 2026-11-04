using Behind.Common;
using Behind.Core.Database;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public static class UserManagementResponseExtensions
{
    extension(IWallOfShameQueries.ActiveBan ban)
    {
        private BanInfo ToBanInfo() => new(ban.Reason, ban.Expires.Match<DateTimeOffset?>(() => null, expires => expires));
    }

    extension(IWallOfShameQueries.ShamedUser shamed)
    {
        public WallOfShameItem ToItem() =>
            new(shamed.DisplayName, shamed.Reason, shamed.Expires.Match<DateTimeOffset?>(() => null, expires => expires));
    }

    extension(IUserManagementQueries.ManagedUser user)
    {
        public ManagedUserItem ToItem(UserManagementActor actor) =>
            new(
                user.Id,
                user.DisplayName,
                user.Emails,
                user.EmailConfirmed,
                user.Roles,
                user.IsAdministrator,
                user.IsDeveloper,
                user.Id == actor.UserId,
                user.Ban.Match<BanInfo?>(() => null, ban => ban.ToBanInfo()),
                !user.IsActive,
                new ManagedUserPermissions(
                    actor.CanChange(ManagedRole.Administrator, user),
                    actor.CanChange(ManagedRole.Developer, user),
                    actor.CanModerate(user),
                    actor.CanDelete(user),
                    actor.CanManageDeleted(user),
                    actor.CanImpersonate(user),
                    actor.CanConfirmEmail(user)));
    }
}
