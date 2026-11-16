using Behind.Core.Domain;
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
                    : displayName is null || emailList.Count == 0
                        ? new UnknownUser(userId)
                        : new HumanUser(displayName, emailList.Select(value => new Email(value)), None);
        }
    }
}
