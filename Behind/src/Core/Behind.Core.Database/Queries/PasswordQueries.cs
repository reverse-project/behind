using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Queries;

public sealed class PasswordQueries(AbstractCoreContext context) : IPasswordQueries
{
    public Task<IEnumerable<Password>> GetPasswordsForUser(User user, CancellationToken cancellationToken = default) =>
        GetPasswordsFromUserId(context.GetKey((HumanUser)user), cancellationToken);

    public async Task<IEnumerable<Password>> GetPasswordsFromUserId(Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = from password in context.Passwords
            where EF.Property<Guid>(password, "UserId") == id
            select password;

        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }
}