using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database;

public static class BuiltInUsersExtensions
{
    extension(AbstractCoreContext context)
    {
        public async Task EnsureBuiltInUsersAsync(CancellationToken cancellationToken = default)
        {
            await context.EnsureBuiltInUserAsync(
                DomainConstants.SystemUserId,
                "System",
                "system@behind.local",
                cancellationToken
            );
            await context.EnsureBuiltInUserAsync(
                DomainConstants.GuestUserId,
                "Guest",
                "guest@behind.local",
                cancellationToken
            );
        }

        private async Task EnsureBuiltInUserAsync(
            Guid id,
            string displayName,
            string email,
            CancellationToken cancellationToken
        )
        {
            var exists = await context.Users.AnyAsync(user => EF.Property<Guid>(user, "Id") == id, cancellationToken);

            if (exists)
            {
                return;
            }

            var user = new HumanUser(displayName, [new Email(email)], None);

            await context.Users.AddAsync(user, cancellationToken);
            context.Entry(user).Property<Guid>("Id").CurrentValue = id;

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
