using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public class EmailCommands(AbstractCoreContext context) : IEmailCommands
{
    public async Task Confirm(string email, CancellationToken cancellationToken = default)
    {
        var query = from _email in context.Emails
            where _email.Value == email.ToLowerInvariant()
            select new { Id = EF.Property<Guid>(_email, "Id"), Email = _email };

        var dbEmail = await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (dbEmail is null)
        {
            return;
        }

        context.GetRepository<Email, Guid>().Update(dbEmail.Id, dbEmail.Email with { IsConfirmed = true });
        await context.SaveChangesAsync(cancellationToken);
    }
}
