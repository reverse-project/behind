using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database;

public static class UserResolutionExtensions
{
    extension(Guid userId)
    {
        public User ToUser(string? displayName, IEnumerable<string> emails)
        {
            var emailList = emails.ToList();

            return userId == DomainConstants.SystemUserId
                ? new SystemUser(None)
                : userId == DomainConstants.GuestUserId
                    ? new GuestUser(None)
                    : displayName is null
                        ? new UnknownUser(userId)
                        : new HumanUser { DisplayName = displayName, Emails = emailList.Select(value => new Email(value)).ToList(), PersistedId = userId };
        }

        public User ToUser(string? displayName, IEnumerable<string> emails, IReadOnlyDictionary<Guid, string> services) =>
            services.TryGetValue(userId, out var name)
                ? new ServiceUser(name, string.Empty, false)
                : userId.ToUser(displayName, emails);
    }

    extension(AbstractCoreContext context)
    {
        public async Task<IReadOnlyDictionary<Guid, string>> ServiceUserNames(
            IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            await context.ServiceUsers.Where(user => ids.Contains(EF.Property<Guid>(user, "Id")))
                .Select(user => new { Id = EF.Property<Guid>(user, "Id"), user.Name })
                .AsNoTracking()
                .ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken);
    }
}
