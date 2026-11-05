using System.Buffers.Text;
using Behind.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using static Behind.Common.FunctionalExtensions;
using TotpSecret = Behind.Core.Common.Configuration.Totp;

namespace Behind.Core.WebApi.TwoFactor;

public interface ITwoFactorVerification
{
    Task<bool> VerifyTotp(Guid userId, string code, CancellationToken cancellationToken = default);

    Task<Option<RecoveryOutcome>> VerifyRecoveryCode(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default);

    Task<Option<AssertionOptions>> BeginPasskey(
        Uri origin,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyPasskey(
        Uri origin,
        Guid userId,
        AssertionOptions options,
        AuthenticatorAssertionRawResponse response,
        CancellationToken cancellationToken = default);
}

public sealed class TwoFactorVerification(
    ITwoFactorQueries queries,
    ITwoFactorCommands commands,
    IRecoveryCodes recoveryCodes,
    IPasskeyCeremonies passkeys,
    TimeProvider timeProvider) : ITwoFactorVerification
{
    public async Task<bool> VerifyTotp(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var stored = await queries.FindTotp(userId, cancellationToken);

        var step = stored
            .Where(totp => totp.IsConfirmed)
            .Bind(totp => totp.Secret.ToTotp())
            .Bind(secret => secret.VerifiedStep(code.WithoutSpaces(), timeProvider.GetUtcNow(), TotpSecret.UserHashMode, TotpSecret.UserTolerance));

        return await step.Match(
            () => Task.FromResult(false),
            matched => commands.AdvanceTotpStep(userId, matched, cancellationToken));
    }

    public Task<Option<RecoveryOutcome>> VerifyRecoveryCode(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default) =>
        recoveryCodes.Redeem(userId, code, cancellationToken);

    public async Task<Option<AssertionOptions>> BeginPasskey(
        Uri origin,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var stored = await queries.GetPasskeys(userId, cancellationToken);

        return stored.Count == 0
            ? None
            : Some(passkeys.BeginAssertion(
                origin,
                stored.Select(passkey => new PublicKeyCredentialDescriptor(Base64Url.DecodeFromChars(passkey.CredentialId)))
                    .ToList()));
    }

    public async Task<bool> VerifyPasskey(
        Uri origin,
        Guid userId,
        AssertionOptions options,
        AuthenticatorAssertionRawResponse response,
        CancellationToken cancellationToken = default)
    {
        var stored = await queries.FindPasskey(userId, Base64Url.EncodeToString(response.RawId), cancellationToken);

        return await stored.Match(
            () => Task.FromResult(false),
            async passkey =>
            {
                var result = await passkeys.CompleteAssertion(
                    origin,
                    options,
                    response,
                    Base64Url.DecodeFromChars(passkey.PublicKey),
                    (uint)passkey.SignCount,
                    userId.ToByteArray(),
                    cancellationToken);

                return await result.Match(
                    () => Task.FromResult(false),
                    async verified =>
                    {
                        if (verified.SignCount != passkey.SignCount)
                        {
                            await commands.UpdatePasskeyCounter(userId, passkey.Id, verified.SignCount, cancellationToken);
                        }

                        return true;
                    });
            });
    }
}
