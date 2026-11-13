using Behind.Core.Database;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.TwoFactor;

public interface ITwoFactorChallengeNonces
{
    Task<TwoFactorTicket> Issue(TwoFactorTicket ticket, CancellationToken cancellationToken = default);
    Task<bool> IsUsable(TwoFactorTicket ticket, CancellationToken cancellationToken = default);
    Task<bool> Consume(TwoFactorTicket ticket, CancellationToken cancellationToken = default);
}

public sealed class TwoFactorChallengeNonces(
    IMagicLinkCommands commands,
    IMagicLinkQueries queries,
    TimeProvider timeProvider) : ITwoFactorChallengeNonces
{
    public async Task<TwoFactorTicket> Issue(TwoFactorTicket ticket, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        if (ticket.Stage is not (TwoFactorStage.Setup or TwoFactorStage.Verify)
            || ticket.Expires <= now
            || ticket.Expires > now.Add(TwoFactorTickets.Lifetime))
        {
            throw new ArgumentException("A current sign-in second-factor ticket is required.", nameof(ticket));
        }

        var nonce = Guid.NewGuid();
        await commands.Issue(
            ticket.UserId,
            new MagicLink(nonce, now, ticket.Expires, MagicLinkFunction.TwoFactorChallenge),
            cancellationToken);
        return ticket with { Nonce = nonce };
    }

    public Task<bool> IsUsable(TwoFactorTicket ticket, CancellationToken cancellationToken = default) =>
        Valid(ticket)
            ? queries.IsUsableForFunction(
                ticket.Nonce!.Value,
                ticket.UserId,
                MagicLinkFunction.TwoFactorChallenge,
                timeProvider.GetUtcNow(),
                cancellationToken)
            : Task.FromResult(false);

    public Task<bool> Consume(TwoFactorTicket ticket, CancellationToken cancellationToken = default) =>
        Valid(ticket)
            ? commands.ConsumeForFunction(
                ticket.Nonce!.Value,
                ticket.UserId,
                MagicLinkFunction.TwoFactorChallenge,
                timeProvider.GetUtcNow(),
                cancellationToken)
            : Task.FromResult(false);

    private bool Valid(TwoFactorTicket ticket) =>
        ticket.Stage is TwoFactorStage.Setup or TwoFactorStage.Verify
        && ticket.Nonce is { } nonce
        && nonce != Guid.Empty
        && ticket.Expires > timeProvider.GetUtcNow();
}
