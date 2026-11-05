using System.Text.Json;
using Behind.Common;
using Behind.Core.Domain;
using Microsoft.AspNetCore.DataProtection;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.TwoFactor;

public interface ITwoFactorTickets
{
    TwoFactorTicket Create(
        Guid userId,
        TwoFactorStage stage,
        MagicLinkFunction? function = null,
        string? ceremony = null);

    string Seal(TwoFactorTicket ticket);

    Option<TwoFactorTicket> Open(string? token);
}

public sealed class TwoFactorTickets(IDataProtectionProvider provider, TimeProvider timeProvider) : ITwoFactorTickets
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string Purpose = "Behind.TwoFactor.Ticket";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public TwoFactorTicket Create(
        Guid userId,
        TwoFactorStage stage,
        MagicLinkFunction? function = null,
        string? ceremony = null) =>
        new(userId, stage, timeProvider.GetUtcNow().Add(Lifetime), function, ceremony);

    public string Seal(TwoFactorTicket ticket) => _protector.Protect(JsonSerializer.Serialize(ticket));

    public Option<TwoFactorTicket> Open(string? token) =>
        string.IsNullOrWhiteSpace(token)
            ? None
            : Try(() => JsonSerializer.Deserialize<TwoFactorTicket>(_protector.Unprotect(token)))
                .Run()
                .Match<Option<TwoFactorTicket>>(_ => None, ticket => ticket.ToOption())
                .Where(ticket => ticket.Expires > timeProvider.GetUtcNow());
}
