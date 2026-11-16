using System.Data;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public class EmailCommands(AbstractCoreContext context, TimeProvider timeProvider) : IEmailCommands
{
    public async Task<IEmailCommands.AddressResult> SaveAddress(
        Guid userId, Guid? id, string value, bool confirmationRequired, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var emails = await EmailsOf(userId, cancellationToken);
        var existing = emails.SingleOrDefault(item => item.Id == id);

        if (id is not null && existing.Email is null)
        {
            return new(IEmailCommands.AddressStatus.NotFound);
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (await context.Emails.AnyAsync(email => email.Value.ToLower() == normalized
            && (id == null || EF.Property<Guid>(email, "Id") != id), cancellationToken))
        {
            return new(IEmailCommands.AddressStatus.InUse);
        }

        if (existing.Email is { } current
            && string.Equals(current.Value, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return new(IEmailCommands.AddressStatus.Saved,
                new(existing.Id, current.Value, current.ConfirmedAt));
        }

        var saved = new Email(normalized, confirmationRequired ? null : timeProvider.GetUtcNow());
        var key = id ?? Guid.CreateVersion7();

        if (id is not null)
        {
            context.GetRepository<Email, Guid>().Update(key, saved);
        }
        else
        {
            await context.Emails.AddAsync(saved, cancellationToken);
            context.Entry(saved).Property<Guid>("Id").CurrentValue = key;
            context.Entry(saved).Property<Guid>("UserId").CurrentValue = userId;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(IEmailCommands.AddressStatus.Saved, new(key, saved.Value, saved.ConfirmedAt));
    }

    public async Task<IEmailCommands.AddressStatus> RemoveAddress(
        Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var emails = await EmailsOf(userId, cancellationToken);

        if (emails.All(item => item.Id != id))
        {
            return IEmailCommands.AddressStatus.NotFound;
        }

        if (emails.Count == 1)
        {
            return IEmailCommands.AddressStatus.LastAddress;
        }

        await context.GetRepository<Email, Guid>().DeleteAsync(id, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return IEmailCommands.AddressStatus.Saved;
    }

    public async Task<bool> ConfirmAddress(
        Guid userId, Guid id, string value, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var email = (await EmailsOf(userId, cancellationToken))
            .SingleOrDefault(item => item.Id == id && item.Email.Value == value);

        if (email.Email is null)
        {
            return false;
        }

        context.GetRepository<Email, Guid>()
            .Update(id, email.Email with { ConfirmedAt = email.Email.ConfirmedAt ?? timeProvider.GetUtcNow() });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

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
