using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public sealed record WallOfShameEntry(User User, Option<DateTimeOffset> Expires, Reason Reason, User GivenBy)
{
    public WallOfShameEntry(Reason Reason) : this(new GuestUser(None), None, Reason, new GuestUser(None))
    {
    }
}

public static class WallOfShameEntryExtensions
{
    extension(WallOfShameEntry wallOfShameEntry)
    {
        public WallOfShameEntry WithUser(User user) =>
            wallOfShameEntry with { User = user };

        public WallOfShameEntry WithExpires(Option<DateTimeOffset> expires) =>
            wallOfShameEntry with { Expires = expires };
        
        public WallOfShameEntry WithGivenBy(User givenBy) =>
            wallOfShameEntry with { GivenBy = givenBy };
    }
}