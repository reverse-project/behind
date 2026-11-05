using System.Security.Cryptography;
using Behind.Common;
using Behind.Core.Common;
using Behind.Core.Database;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.TwoFactor;

public interface IRecoveryCodes
{
    Task<IReadOnlyList<string>> Issue(Guid userId, CancellationToken cancellationToken = default);

    Task<Option<RecoveryOutcome>> Redeem(Guid userId, string code, CancellationToken cancellationToken = default);
}

public sealed class RecoveryCodes(ITwoFactorQueries queries, ITwoFactorCommands commands) : IRecoveryCodes
{
    public async Task<IReadOnlyList<string>> Issue(Guid userId, CancellationToken cancellationToken = default)
    {
        var codes = RandomNumberGenerator.CreateRecoveryCodes();
        var hashes = await Task.Run(
            () => codes.AsParallel().AsOrdered().Select(code => code.ToRecoveryCodeHash()).ToList(),
            cancellationToken);

        await commands.ReplaceRecoveryCodes(userId, hashes, cancellationToken);

        return codes;
    }

    public async Task<Option<RecoveryOutcome>> Redeem(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (!code.IsRecoveryCodeShape)
        {
            return None;
        }

        var stored = await queries.GetRecoveryCodes(userId, cancellationToken);
        var matched = await Task.Run(
            () => stored.AsParallel().FirstOrDefault(candidate => code.MatchesRecoveryCodeHash(candidate.Hash)),
            cancellationToken);

        if (matched is null || !await commands.ConsumeRecoveryCode(userId, matched.Id, cancellationToken))
        {
            return None;
        }

        return Some(new RecoveryOutcome(
            stored.Count > 1 ? [] : await Issue(userId, cancellationToken)));
    }
}
