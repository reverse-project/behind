using Behind.Common;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class TwoFactorQueries(AbstractCoreContext context) : ITwoFactorQueries
{
    public async Task<ITwoFactorQueries.TwoFactorStatus> GetStatus(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var totp = await FindTotp(userId, cancellationToken);

        return new ITwoFactorQueries.TwoFactorStatus(
            totp.Map(info => info.IsConfirmed).GetOrElse(false),
            await GetPasskeys(userId, cancellationToken),
            await context.RecoveryCodesOf(userId).CountAsync(cancellationToken));
    }

    public async Task<Option<ITwoFactorQueries.TotpInfo>> FindTotp(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var found = await context.TotpsOf(userId)
            .Select(totp => new ITwoFactorQueries.TotpInfo(totp.Secret, totp.IsConfirmed, totp.LastUsedStep))
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return found is null ? None : Some(found);
    }

    public async Task<IReadOnlyList<ITwoFactorQueries.PasskeyInfo>> GetPasskeys(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.PasskeysOf(userId)
            .Select(passkey => new
            {
                Id = EF.Property<Guid>(passkey, "Id"),
                passkey.Name,
                passkey.CredentialId,
                passkey.PublicKey,
                passkey.SignCount
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var created = await CreationTimes(rows.Select(row => row.Id.ToString()).ToList(), cancellationToken);

        return rows
            .Select(row => new ITwoFactorQueries.PasskeyInfo(
                row.Id,
                row.Name,
                row.CredentialId,
                row.PublicKey,
                row.SignCount,
                created.TryGetValue(row.Id.ToString(), out var at) ? Some(at) : None))
            .OrderBy(passkey => passkey.CreatedAt.GetOrElse(DateTimeOffset.MaxValue))
            .ThenBy(passkey => passkey.Id)
            .ToList();
    }

    public async Task<Option<ITwoFactorQueries.PasskeyInfo>> FindPasskey(
        Guid userId,
        string credentialId,
        CancellationToken cancellationToken = default) =>
        (await GetPasskeys(userId, cancellationToken))
        .Where(passkey => passkey.CredentialId == credentialId)
        .Head();

    public Task<bool> IsCredentialRegistered(string credentialId, CancellationToken cancellationToken = default) =>
        context.UserPasskeys.AnyAsync(passkey => passkey.CredentialId == credentialId, cancellationToken);

    public async Task<IReadOnlyList<ITwoFactorQueries.RecoveryCodeInfo>> GetRecoveryCodes(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await context.RecoveryCodesOf(userId)
            .Select(code => new ITwoFactorQueries.RecoveryCodeInfo(EF.Property<Guid>(code, "Id"), code.Hash))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyDictionary<string, DateTimeOffset>> CreationTimes(
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken)
    {
        var entries = await context.AuditLogEntries
            .Where(entry => entry.EntityName == nameof(UserPasskey) && keys.Contains(entry.EntityKey))
            .Select(entry => new { entry.EntityKey, entry.Action, entry.Timestamp })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entries
            .Where(entry => entry.Action is AuditLogEntry.Created)
            .GroupBy(entry => entry.EntityKey)
            .ToDictionary(group => group.Key, group => group.Min(entry => entry.Timestamp));
    }
}
