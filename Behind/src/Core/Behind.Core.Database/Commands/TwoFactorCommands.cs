using Behind.Common.Database.Implementation;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class TwoFactorCommands(AbstractCoreContext context) : ITwoFactorCommands
{
    public async Task SaveTotp(Guid userId, string secret, CancellationToken cancellationToken = default)
    {
        await RemoveTotpRows(userId, cancellationToken);

        var totp = new UserTotp(secret);
        var entry = await context.UserTotps.AddAsync(totp, cancellationToken);
        entry.Property<Guid>(UserTotpConfiguration.UserIdProperty).CurrentValue = userId;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ConfirmTotp(Guid userId, long step, CancellationToken cancellationToken = default)
    {
        var rows = await context.TotpsOf(userId)
            .Where(totp => !totp.IsConfirmed)
            .Select(totp => new { Id = EF.Property<Guid>(totp, "Id"), Totp = totp })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var row = rows.FirstOrDefault();

        if (row is null)
        {
            return false;
        }

        context.UpdateImmutable([row.Id], row.Totp with { IsConfirmed = true, LastUsedStep = step });
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> AdvanceTotpStep(Guid userId, long step, CancellationToken cancellationToken = default) =>
        await context.TotpsOf(userId)
            .Where(totp => totp.IsConfirmed && totp.LastUsedStep < step)
            .ExecuteUpdateAsync(setters => setters.SetProperty(totp => totp.LastUsedStep, step), cancellationToken)
        > 0;

    public async Task<bool> RemoveTotp(Guid userId, CancellationToken cancellationToken = default)
    {
        var removed = await RemoveTotpRows(userId, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return removed > 0;
    }

    public async Task AddPasskey(Guid userId, UserPasskey passkey, CancellationToken cancellationToken = default)
    {
        var entry = await context.UserPasskeys.AddAsync(passkey, cancellationToken);
        entry.Property<Guid>(UserPasskeyConfiguration.UserIdProperty).CurrentValue = userId;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdatePasskeyCounter(
        Guid userId,
        Guid passkeyId,
        long signCount,
        CancellationToken cancellationToken = default) =>
        await context.PasskeysOf(userId)
            .Where(passkey => EF.Property<Guid>(passkey, "Id") == passkeyId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(passkey => passkey.SignCount, signCount), cancellationToken)
        > 0;

    public async Task<bool> RemovePasskey(Guid userId, Guid passkeyId, CancellationToken cancellationToken = default)
    {
        var rows = await context.PasskeysOf(userId)
            .Where(passkey => EF.Property<Guid>(passkey, "Id") == passkeyId)
            .ToListAsync(cancellationToken);

        context.UserPasskeys.RemoveRange(rows);
        await context.SaveChangesAsync(cancellationToken);

        return rows.Count > 0;
    }

    public async Task ReplaceRecoveryCodes(
        Guid userId,
        IReadOnlyList<string> hashes,
        CancellationToken cancellationToken = default)
    {
        context.UserRecoveryCodes.RemoveRange(await context.RecoveryCodesOf(userId).ToListAsync(cancellationToken));

        foreach (var hash in hashes)
        {
            var entry = await context.UserRecoveryCodes.AddAsync(new UserRecoveryCode(hash), cancellationToken);
            entry.Property<Guid>(UserRecoveryCodeConfiguration.UserIdProperty).CurrentValue = userId;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ConsumeRecoveryCode(
        Guid userId,
        Guid codeId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.RecoveryCodesOf(userId)
            .Where(code => EF.Property<Guid>(code, "Id") == codeId)
            .ToListAsync(cancellationToken);

        context.UserRecoveryCodes.RemoveRange(rows);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();

            return false;
        }

        return rows.Count > 0;
    }

    public async Task RemoveRecoveryCodes(Guid userId, CancellationToken cancellationToken = default)
    {
        context.UserRecoveryCodes.RemoveRange(await context.RecoveryCodesOf(userId).ToListAsync(cancellationToken));

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> RemoveTotpRows(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await context.TotpsOf(userId).ToListAsync(cancellationToken);

        context.UserTotps.RemoveRange(rows);

        return rows.Count;
    }
}
