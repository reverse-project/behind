
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using OtpNet;

namespace Behind.Core.WebApi.TwoFactor;

public sealed record EmailTwoFactorAddress(string Id, string Display);

public sealed record EmailTwoFactorAddresses(IReadOnlyList<EmailTwoFactorAddress> Addresses, string? Error = null);

public sealed record EmailTwoFactorDelivery(string? Ticket = null, string? Address = null, string? Error = null);

public sealed record EmailTwoFactorVerification(TwoFactorTicket? Ticket = null, string? Error = null);

public interface IEmailTwoFactor
{
    Task<EmailTwoFactorAddresses> GetAddresses(ApplicationUser user, TwoFactorTicket ticket, CancellationToken cancellationToken = default);
    Task<EmailTwoFactorDelivery> Begin(ApplicationUser user, TwoFactorTicket ticket, string addressId, CancellationToken cancellationToken = default);
    Task<EmailTwoFactorVerification> Verify(ApplicationUser user, TwoFactorTicket ticket, string code, CancellationToken cancellationToken = default);
}

public sealed class EmailTwoFactor(
    ITwoFactorTickets tickets,
    ITwoFactorQueries queries,
    IMagicLinkCommands commands,
    ITwoFactorChallengeNonces challengeNonces,
    IEmailSender emailSender,
    IOptionsSnapshot<AccountsConfiguration> accounts,
    IOptions<SecurityConfiguration> security,
    TimeProvider timeProvider,
    ILogger<EmailTwoFactor> logger) : IEmailTwoFactor
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan DeliveryWindow = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan VerificationWindow = TimeSpan.FromMinutes(5);
    public const int VerificationLimit = 5;
    public const string Unavailable = "emailTwoFactorUnavailable";
    public const string DeliveryFailed = "emailDeliveryFailed";
    public const string RateLimited = "rateLimited";

    public static IReadOnlyList<string> ConfirmedAddresses(ApplicationUser user) =>
        user.Emails
            .Where(email => email.ConfirmedAt is not null && !string.IsNullOrWhiteSpace(email.Value))
            .Select(email => email.Value)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ThenBy(address => address, StringComparer.Ordinal)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static bool IndependentOf(MagicLinkFunction? function) =>
        function is null or MagicLinkFunction.PasskeyChallenge;

    public async Task<EmailTwoFactorAddresses> GetAddresses(
        ApplicationUser user,
        TwoFactorTicket ticket,
        CancellationToken cancellationToken = default)
    {
        ticket = Parent(ticket);

        if (!await Eligible(user, ticket, cancellationToken))
        {
            return new([], Unavailable);
        }

        return new(ConfirmedAddresses(user).Select(address =>
            new EmailTwoFactorAddress(
                tickets.Seal(ticket with
                {
                    Ceremony = JsonSerializer.Serialize(new Selection(address, ticket.Ceremony))
                }),
                Mask(address))).ToList());
    }

    public async Task<EmailTwoFactorDelivery> Begin(
        ApplicationUser user,
        TwoFactorTicket ticket,
        string addressId,
        CancellationToken cancellationToken = default)
    {
        ticket = Parent(ticket);

        if (!await Eligible(user, ticket, cancellationToken))
        {
            return new(Error: Unavailable);
        }

        var selected = tickets.Open(addressId).Match<TwoFactorTicket?>(() => null, found => found);
        var selection = Read<Selection>(selected?.Ceremony);

        if (selected is null
            || selection is null
            || (selected with { Ceremony = selection.ParentCeremony }) != ticket
            || !ConfirmedAddresses(user).Contains(selection.Address, StringComparer.Ordinal))
        {
            return new(Error: TwoFactorFailure.TicketInvalid);
        }

        var now = timeProvider.GetUtcNow();

        if (!await Reserve(user.Id, "delivery", DeliveryWindow, 1, now, cancellationToken))
        {
            return new(Error: RateLimited);
        }

        var expires = now.Add(Lifetime);
        if (expires > ticket.Expires)
        {
            expires = ticket.Expires;
        }

        var link = new MagicLink(Guid.NewGuid(), now, expires, MagicLinkFunction.EmailTwoFactor);
        var secret = RandomNumberGenerator.GetBytes(32);
        var payload = new Delivery(
            link.Nonce.Nonce,
            Convert.ToBase64String(secret),
            selection.Address,
            now,
            ticket.Expires,
            ticket.Ceremony);
        await commands.Issue(user.Id, link, cancellationToken);

        try
        {
            await emailSender.SendEmailAsync(
                selection.Address,
                "Your two-factor sign in code",
                $"<p>Your two-factor sign in code is <strong>{WebUtility.HtmlEncode(Code(secret, now))}</strong>.</p><p>This code expires in three minutes and can only be used once.</p>");
        }
        catch (Exception exception) when (exception is SmtpException or IOException)
        {
            logger.LogWarning(exception, "Two-factor email delivery failed for user {UserId}", user.Id);
            await commands.ConsumeForFunction(link.Nonce, user.Id, MagicLinkFunction.EmailTwoFactor, now, cancellationToken);
            return new(Error: DeliveryFailed);
        }

        return new(
            tickets.Seal(ticket with { Expires = expires, Ceremony = JsonSerializer.Serialize(payload) }),
            Mask(selection.Address));
    }

    public async Task<EmailTwoFactorVerification> Verify(
        ApplicationUser user,
        TwoFactorTicket ticket,
        string code,
        CancellationToken cancellationToken = default)
    {
        var payload = Read<Delivery>(ticket.Ceremony);
        var now = timeProvider.GetUtcNow();

        if (payload is null
            || payload.Nonce == Guid.Empty
            || payload.IssuedAt > now
            || now >= payload.IssuedAt.Add(Lifetime)
            || payload.ParentExpires < ticket.Expires
            || !await Eligible(user, ticket, cancellationToken)
            || !ConfirmedAddresses(user).Contains(payload.Address, StringComparer.Ordinal))
        {
            return new(Error: TwoFactorFailure.TicketInvalid);
        }

        if (!await Reserve(user.Id, "verification", VerificationWindow, VerificationLimit, now, cancellationToken))
        {
            return new(Error: RateLimited);
        }

        var secret = ReadSecret(payload.Secret);
        if (secret is null
            || code is null
            || code.Length != 6
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(code),
                Encoding.UTF8.GetBytes(Code(secret, payload.IssuedAt)))
            || !await commands.ConsumeForFunction(
                payload.Nonce, user.Id, MagicLinkFunction.EmailTwoFactor, now, cancellationToken))
        {
            return new(Error: TwoFactorFailure.InvalidCode);
        }

        var parent = ticket with { Expires = payload.ParentExpires, Ceremony = payload.ParentCeremony };
        return new(parent);
    }

    private async Task<bool> Eligible(
        ApplicationUser user,
        TwoFactorTicket ticket,
        CancellationToken cancellationToken)
    {
        if (user.Id != ticket.UserId
            || ticket.Expires <= timeProvider.GetUtcNow()
            || ticket.Stage is not (TwoFactorStage.Verify or TwoFactorStage.Setup)
            || !IndependentOf(ticket.Function)
            || !accounts.Value.TwoFactor
            || !accounts.Value.EmailTwoFactor
            || ConfirmedAddresses(user).Count == 0
            || !await challengeNonces.IsUsable(ticket, cancellationToken))
        {
            return false;
        }

        var status = await queries.GetStatus(user.Id, cancellationToken);
        return ticket.Stage == TwoFactorStage.Verify
            ? status.IsEnabled || security.Value.TwoFactorRequired
            : security.Value.TwoFactorRequired && !status.IsEnabled;
    }

    private async Task<bool> Reserve(
        Guid userId,
        string operation,
        TimeSpan window,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var windowSeconds = (long)window.TotalSeconds;
        var bucket = now.ToUnixTimeSeconds() / windowSeconds;
        var expires = DateTimeOffset.FromUnixTimeSeconds((bucket + 1) * windowSeconds);

        for (var slot = 0; slot < limit; slot++)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"Behind.EmailTwoFactor:{userId:N}:{operation}:{bucket}:{slot}"));
            var nonce = new Guid(hash.AsSpan(0, 16));
            if (await commands.TryIssue(
                    userId,
                    new MagicLink(nonce, now, expires, MagicLinkFunction.EmailTwoFactor),
                    cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static string Code(byte[] secret, DateTimeOffset issuedAt) =>
        new OtpNet.Totp(secret, step: (int)Lifetime.TotalSeconds, mode: OtpHashMode.Sha256)
            .ComputeTotp(issuedAt.UtcDateTime);

    private static string Mask(string address)
    {
        var separator = address.LastIndexOf('@');
        return separator > 0 ? $"{address[0]}***{address[separator..]}" : "***";
    }

    private TwoFactorTicket Parent(TwoFactorTicket ticket)
    {
        var payload = Read<Delivery>(ticket.Ceremony);
        return payload is not null && payload.Nonce != Guid.Empty && ticket.Expires > timeProvider.GetUtcNow()
            ? ticket with { Expires = payload.ParentExpires, Ceremony = payload.ParentCeremony }
            : ticket;
    }

    private static T? Read<T>(string? value) where T : class
    {
        try
        {
            return value is null ? null : JsonSerializer.Deserialize<T>(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static byte[]? ReadSecret(string? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            var secret = Convert.FromBase64String(value);
            return secret.Length == 32 ? secret : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private sealed record Selection(string Address, string? ParentCeremony);

    private sealed record Delivery(
        Guid Nonce,
        string Secret,
        string Address,
        DateTimeOffset IssuedAt,
        DateTimeOffset ParentExpires,
        string? ParentCeremony);
}
