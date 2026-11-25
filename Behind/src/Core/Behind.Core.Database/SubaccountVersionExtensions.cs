using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class SubaccountVersionExtensions
{
    extension(AbstractCoreContext context)
    {
        public IQueryable<Subaccount> LatestSubaccounts() =>
            context.Subaccounts.LatestOf(context.Subaccounts);

        public IQueryable<Subaccount> ActiveSubaccounts() =>
            context.LatestSubaccounts().Where(link => link.IsActive);

        public Subaccount AppendSubaccountVersion(Subaccount current, bool? isChild = null, bool? isActive = null)
        {
            var previousId = context.Entry(current).VersionedId;
            var id = new VersionedKey<Guid>(previousId.Key, checked(previousId.Version + 1));
            var next = current.NextVersion(isChild ?? current.IsChild, isActive ?? current.IsActive);
            context.Subaccounts.Add(next);
            var currentEntry = context.Entry(current);
            var nextEntry = context.Entry(next);
            nextEntry.StampVersionedKey(id.Key, id.Version);
            nextEntry.Property<Guid>("PrimaryUserId").CurrentValue =
                currentEntry.Property<Guid>("PrimaryUserId").CurrentValue;
            nextEntry.Property<Guid>("UserId").CurrentValue =
                currentEntry.Property<Guid>("UserId").CurrentValue;
            return next;
        }
    }
}
