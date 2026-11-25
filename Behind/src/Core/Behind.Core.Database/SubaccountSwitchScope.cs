using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public sealed record SubaccountSwitchScope(Guid AnchorId, bool FromLinkedProfile)
{
    public static Task<bool> IsLinked(AbstractCoreContext db, Guid userId, CancellationToken cancellationToken) =>
        db.ActiveSubaccounts().AnyAsync(link => EF.Property<Guid>(link, "UserId") == userId, cancellationToken);

    public IQueryable<HumanUser> Profiles(AbstractCoreContext db) =>
        db.Users.InDeletionState(db, AbstractCoreContext.DeletionRecorded, DeletionState.Active)
            .Where(user => EF.Property<Guid>(user, "Id") == AnchorId || db.ActiveSubaccounts().Any(link => FromLinkedProfile
                ? EF.Property<Guid>(link, "UserId") == AnchorId
                    && EF.Property<Guid>(link, "PrimaryUserId") == EF.Property<Guid>(user, "Id")
                : EF.Property<Guid>(link, "PrimaryUserId") == AnchorId
                    && EF.Property<Guid>(link, "UserId") == EF.Property<Guid>(user, "Id")));
}
