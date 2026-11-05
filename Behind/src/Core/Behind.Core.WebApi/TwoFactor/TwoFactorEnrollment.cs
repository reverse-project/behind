using System.Buffers.Text;
using Behind.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;
using TotpSecret = Behind.Core.Common.Configuration.Totp;

namespace Behind.Core.WebApi.TwoFactor;

public interface ITwoFactorEnrollment
{
    Task<Option<TotpSetup>> BeginTotp(ApplicationUser user, CancellationToken cancellationToken = default);

    Task<Option<Enrolled>> ConfirmTotp(Guid userId, string code, CancellationToken cancellationToken = default);

    Task<PasskeyRegistrationChallenge> BeginPasskey(
        Uri origin,
        ApplicationUser user,
        TwoFactorStage stage,
        MagicLinkFunction? function,
        CancellationToken cancellationToken = default);

    Task<Option<Enrolled>> CompletePasskey(
        Uri origin,
        TwoFactorTicket ticket,
        string? name,
        AuthenticatorAttestationRawResponse response,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> RegenerateRecoveryCodes(Guid userId, CancellationToken cancellationToken = default);

    Task<TwoFactorRemoval> RemoveTotp(Guid userId, CancellationToken cancellationToken = default);

    Task<TwoFactorRemoval> RemovePasskey(Guid userId, Guid passkeyId, CancellationToken cancellationToken = default);
}

public sealed class TwoFactorEnrollment(
    ITwoFactorQueries queries,
    ITwoFactorCommands commands,
    IRecoveryCodes recoveryCodes,
    IPasskeyCeremonies passkeys,
    ITwoFactorTickets tickets,
    IOptions<SecurityConfiguration> security,
    TimeProvider timeProvider) : ITwoFactorEnrollment
{
    public const string DefaultPasskeyName = "Passkey";

    public async Task<Option<TotpSetup>> BeginTotp(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if ((await queries.GetStatus(user.Id, cancellationToken)).TotpEnabled)
        {
            return None;
        }

        var secret = TotpSecret.Generate();
        var uri = secret.ToOtpUri(user.Account, TotpSecret.Issuer, TotpSecret.UserHashMode);

        await commands.SaveTotp(user.Id, secret.Value, cancellationToken);

        return Some(new TotpSetup(secret.Value, uri, uri.ToQrCodeSvg().GetOrElse(string.Empty)));
    }

    public async Task<Option<Enrolled>> ConfirmTotp(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var stored = await queries.FindTotp(userId, cancellationToken);

        var step = stored
            .Where(totp => !totp.IsConfirmed)
            .Bind(totp => totp.Secret.ToTotp())
            .Bind(secret => secret.VerifiedStep(code.WithoutSpaces(), timeProvider.GetUtcNow(), TotpSecret.UserHashMode, TotpSecret.UserTolerance));

        return await step.Match(
            () => Task.FromResult<Option<Enrolled>>(None),
            async matched => await commands.ConfirmTotp(userId, matched, cancellationToken)
                ? Some(await Enabled(userId, cancellationToken))
                : None);
    }

    public async Task<PasskeyRegistrationChallenge> BeginPasskey(
        Uri origin,
        ApplicationUser user,
        TwoFactorStage stage,
        MagicLinkFunction? function,
        CancellationToken cancellationToken = default)
    {
        var existing = (await queries.GetPasskeys(user.Id, cancellationToken))
            .Select(passkey => new PublicKeyCredentialDescriptor(Base64Url.DecodeFromChars(passkey.CredentialId)))
            .ToList();

        var options = passkeys.BeginRegistration(
            origin,
            new Fido2User { Id = user.Id.ToByteArray(), Name = user.Account, DisplayName = user.FriendlyName },
            existing);

        return new PasskeyRegistrationChallenge(
            tickets.Seal(tickets.Create(user.Id, stage, function, options.ToJson())),
            options);
    }

    public async Task<Option<Enrolled>> CompletePasskey(
        Uri origin,
        TwoFactorTicket ticket,
        string? name,
        AuthenticatorAttestationRawResponse response,
        CancellationToken cancellationToken = default)
    {
        var options = Try(() => CredentialCreateOptions.FromJson(ticket.Ceremony))
            .Run()
            .Match<Option<CredentialCreateOptions>>(_ => None, parsed => parsed);

        return await options.Match(
            () => Task.FromResult<Option<Enrolled>>(None),
            async parsed =>
            {
                var registered = await passkeys.CompleteRegistration(
                    origin,
                    parsed,
                    response,
                    async credentialId => !await queries.IsCredentialRegistered(
                        Base64Url.EncodeToString(credentialId),
                        cancellationToken),
                    cancellationToken);

                return await registered.Match(
                    () => Task.FromResult<Option<Enrolled>>(None),
                    async credential =>
                    {
                        await commands.AddPasskey(
                            ticket.UserId,
                            new UserPasskey(
                                name.ToPasskeyName(),
                                Base64Url.EncodeToString(credential.Id),
                                Base64Url.EncodeToString(credential.PublicKey),
                                credential.SignCount),
                            cancellationToken);

                        return Some(await Enabled(ticket.UserId, cancellationToken));
                    });
            });
    }

    public Task<IReadOnlyList<string>> RegenerateRecoveryCodes(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        recoveryCodes.Issue(userId, cancellationToken);

    public async Task<TwoFactorRemoval> RemoveTotp(Guid userId, CancellationToken cancellationToken = default)
    {
        if (await queries.FindTotp(userId, cancellationToken) == None)
        {
            return TwoFactorRemoval.NotFound;
        }

        var status = await queries.GetStatus(userId, cancellationToken);
        var last = status.TotpEnabled && status.Passkeys.Count == 0;

        if (last && security.Value.TwoFactorRequired)
        {
            return TwoFactorRemoval.Required;
        }

        await commands.RemoveTotp(userId, cancellationToken);
        await DropRecoveryCodesWhen(last, userId, cancellationToken);

        return TwoFactorRemoval.Removed;
    }

    public async Task<TwoFactorRemoval> RemovePasskey(
        Guid userId,
        Guid passkeyId,
        CancellationToken cancellationToken = default)
    {
        var status = await queries.GetStatus(userId, cancellationToken);

        if (status.Passkeys.All(passkey => passkey.Id != passkeyId))
        {
            return TwoFactorRemoval.NotFound;
        }

        var last = !status.TotpEnabled && status.Passkeys.Count == 1;

        if (last && security.Value.TwoFactorRequired)
        {
            return TwoFactorRemoval.Required;
        }

        await commands.RemovePasskey(userId, passkeyId, cancellationToken);
        await DropRecoveryCodesWhen(last, userId, cancellationToken);

        return TwoFactorRemoval.Removed;
    }

    private async Task<Enrolled> Enabled(Guid userId, CancellationToken cancellationToken)
    {
        var status = await queries.GetStatus(userId, cancellationToken);

        return new Enrolled(status.RecoveryCodesRemaining == 0 ? await recoveryCodes.Issue(userId, cancellationToken) : []);
    }

    private async Task DropRecoveryCodesWhen(bool lastMethodRemoved, Guid userId, CancellationToken cancellationToken)
    {
        if (lastMethodRemoved)
        {
            await commands.RemoveRecoveryCodes(userId, cancellationToken);
        }
    }
}
