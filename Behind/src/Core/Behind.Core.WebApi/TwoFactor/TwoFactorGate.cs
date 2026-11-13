using Behind.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.TwoFactor;

public interface ITwoFactorGate
{
    Task<Option<TwoFactorChallengeResponse>> Challenge(
        Guid userId,
        MagicLinkFunction? function,
        CancellationToken cancellationToken = default);

    Task<Option<TwoFactorChallengeResponse>> ChallengePasskey(
        Guid userId,
        string credentialId,
        CancellationToken cancellationToken = default);
}

public sealed class TwoFactorGate(
    ITwoFactorQueries queries,
    IEmailQueries emails,
    IMagicLinkCommands commands,
    TimeProvider timeProvider,
    ITwoFactorTickets tickets,
    IOptions<SecurityConfiguration> security,
    IOptionsSnapshot<AccountsConfiguration> accounts) : ITwoFactorGate
{
    private static readonly IReadOnlyList<TwoFactorMethod> SetupMethods =
        [TwoFactorMethod.Totp, TwoFactorMethod.Passkey];

    private static bool AllowsEmail(MagicLinkFunction? function) =>
        function is not (MagicLinkFunction.SignIn or MagicLinkFunction.LostPassword or MagicLinkFunction.PasswordReset);

    public async Task<Option<TwoFactorChallengeResponse>> Challenge(
        Guid userId,
        MagicLinkFunction? function,
        CancellationToken cancellationToken = default) =>
        await ChallengeCore(userId, function, null, cancellationToken);

    public async Task<Option<TwoFactorChallengeResponse>> ChallengePasskey(
        Guid userId,
        string credentialId,
        CancellationToken cancellationToken = default) =>
        await ChallengeCore(userId, null, credentialId, cancellationToken);

    private async Task<Option<TwoFactorChallengeResponse>> ChallengeCore(
        Guid userId,
        MagicLinkFunction? function,
        string? primaryCredentialId,
        CancellationToken cancellationToken)
    {
        var status = await queries.GetStatus(userId, cancellationToken);
        var settings = accounts.Value;
        var emailAvailable = settings.EmailTwoFactor && AllowsEmail(function)
            && (await emails.Addresses(userId, cancellationToken)).Any(email => email.ConfirmedAt is not null);

        if ((function == MagicLinkFunction.SignIn && !settings.MagicLink)
            || (!settings.TwoFactor && (status.IsEnabled || security.Value.TwoFactorRequired)))
        {
            return Some(new TwoFactorChallengeResponse(string.Empty, [], false, function) { Unavailable = true });
        }

        if (status.IsEnabled)
        {
            var methods = status.Methods.Where(method => method != TwoFactorMethod.Passkey
                || (settings.Passkey && status.Passkeys.Any(passkey => passkey.CredentialId != primaryCredentialId))).ToList();
            if (emailAvailable)
            {
                methods.Add(TwoFactorMethod.Email);
            }

            if (methods.Count == 0)
            {
                return Some(new TwoFactorChallengeResponse(string.Empty, [], false, function) { Unavailable = true });
            }

            return Some(await Challenge(userId, TwoFactorStage.Verify, methods, false, function, primaryCredentialId, cancellationToken));
        }

        if (!security.Value.TwoFactorRequired)
        {
            return None;
        }

        var setupMethods = SetupMethods
            .Where(method => settings.Passkey || method != TwoFactorMethod.Passkey)
            .ToList();
        if (emailAvailable)
        {
            setupMethods.Add(TwoFactorMethod.Email);
        }

        return Some(await Challenge(userId, TwoFactorStage.Setup, setupMethods, true, function, primaryCredentialId, cancellationToken));
    }

    private async Task<TwoFactorChallengeResponse> Challenge(
        Guid userId,
        TwoFactorStage stage,
        IReadOnlyList<TwoFactorMethod> methods,
        bool setupRequired,
        MagicLinkFunction? function,
        string? primaryCredentialId,
        CancellationToken cancellationToken)
    {
        var link = MagicLink.Create(timeProvider.GetUtcNow(), TwoFactorTickets.Lifetime, MagicLinkFunction.TwoFactorChallenge);
        await commands.Issue(userId, link, cancellationToken);
        var ticket = tickets.Create(userId, stage, function) with
        {
            PrimaryCredentialId = primaryCredentialId,
            Nonce = link.Nonce.Nonce
        };
        return new TwoFactorChallengeResponse(tickets.Seal(ticket), methods, setupRequired, function);
    }
}
