using Behind.Common;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Queries;

public sealed class EmailQueries(AbstractCoreContext context) : IEmailQueries
{
    public async Task<Option<Email>> GetEmailByValue(string value, CancellationToken cancellationToken = default)
    {
        var query = from dbEmail in context.Emails
            where dbEmail.Value == value.ToLowerInvariant()
            select dbEmail;

        var email = await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken: cancellationToken);

        return email is null ? None : email;
    }

    public async Task<IEnumerable<Email>> GetEmailsByUser(User user, CancellationToken cancellationToken = default) =>
        await GetEmailsByUserId(await context.GetUserKeyAsync(user, cancellationToken), cancellationToken);

    public async Task<IEnumerable<Email>> GetEmailsByUserId(Guid id, CancellationToken cancellationToken = default)
    {
        var query = from email in context.Emails
            where EF.Property<Guid>(email, "UserId") == id
            orderby EF.Property<Guid>(email, "Id")
            select email;

        return await query.AsNoTracking().ToListAsync(cancellationToken: cancellationToken);
    }
}