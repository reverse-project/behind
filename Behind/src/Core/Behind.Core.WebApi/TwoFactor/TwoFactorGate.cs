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
}

public sealed class TwoFactorGate(
    ITwoFactorQueries queries,
    ITwoFactorTickets tickets,
    IOptions<SecurityConfiguration> security) : ITwoFactorGate
{
    private static readonly IReadOnlyList<TwoFactorMethod> SetupMethods =
        [TwoFactorMethod.Totp, TwoFactorMethod.Passkey];

    public async Task<Option<TwoFactorChallengeResponse>> Challenge(
        Guid userId,
        MagicLinkFunction? function,
        CancellationToken cancellationToken = default)
    {
        var status = await queries.GetStatus(userId, cancellationToken);

        if (status.IsEnabled)
        {
            return Some(Challenge(userId, TwoFactorStage.Verify, status.Methods, false, function));
        }

        return security.Value.TwoFactorRequired
            ? Some(Challenge(userId, TwoFactorStage.Setup, SetupMethods, true, function))
            : None;
    }

    private TwoFactorChallengeResponse Challenge(
        Guid userId,
        TwoFactorStage stage,
        IReadOnlyList<TwoFactorMethod> methods,
        bool setupRequired,
        MagicLinkFunction? function) =>
        new(tickets.Seal(tickets.Create(userId, stage, function)), methods, setupRequired, function);
}
