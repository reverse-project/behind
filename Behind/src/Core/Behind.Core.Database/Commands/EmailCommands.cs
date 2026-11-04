using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public class EmailCommands(AbstractCoreContext context, TimeProvider timeProvider) : IEmailCommands
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

        context.GetRepository<Email, Guid>().Update(dbEmail.Id, dbEmail.Email with { ConfirmedAt = dbEmail.Email.ConfirmedAt ?? timeProvider.GetUtcNow() });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePrimary(Guid userId, string email, CancellationToken cancellationToken = default)
    {
        var value = email.ToLowerInvariant();
        var emails = await EmailsOf(userId, cancellationToken);

        if (emails.Any(existing => existing.Email.Value == value))
        {
            return;
        }

        if (emails is [var primary, ..])
        {
            context.GetRepository<Email, Guid>()
                .Update(primary.Id, primary.Email with { Value = value, ConfirmedAt = null });
            return;
        }

        var created = new Email(value);
        await context.Emails.AddAsync(created, cancellationToken);
        context.Entry(created).Property<Guid>("UserId").CurrentValue = userId;
    }

    public async Task ConfirmPrimary(Guid userId, CancellationToken cancellationToken = default)
    {
        await SetConfirmed(userId, true, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetConfirmed(Guid userId, bool confirmed, CancellationToken cancellationToken = default)
    {
        var emails = await EmailsOf(userId, cancellationToken);

        if (emails is [var primary, ..])
        {
            context.GetRepository<Email, Guid>().Update(primary.Id, primary.Email with { ConfirmedAt = confirmed ? primary.Email.ConfirmedAt ?? timeProvider.GetUtcNow() : null });
        }
    }

    private async Task<List<(Guid Id, Email Email)>> EmailsOf(Guid userId, CancellationToken cancellationToken)
    {
        var emails = await (
            from _email in context.Emails
            where EF.Property<Guid>(_email, "UserId") == userId
            orderby EF.Property<Guid>(_email, "Id")
            select new { Id = EF.Property<Guid>(_email, "Id"), Email = _email }
        ).AsNoTracking().ToListAsync(cancellationToken);

        return emails.Select(item => (item.Id, item.Email)).ToList();
    }
}
