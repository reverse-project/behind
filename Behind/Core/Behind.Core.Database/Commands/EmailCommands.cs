using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public class EmailCommands(AbstractCoreContext context) : IEmailCommands
{
    public async Task Confirm(string email, CancellationToken cancellationToken = default)
    {
        var query = from _email in context.Emails
            where email.Equals(_email.Value, StringComparison.InvariantCultureIgnoreCase)
            select _email;

        var dbEmail = await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (dbEmail is null)
        {
            return;
        }

        var repository = context.GetRepository<Email, Guid>();
        var updated = dbEmail with { IsConfirmed = true };
        var key = repository.GetKey(dbEmail);
        repository.Update(key, updated);
    }
}