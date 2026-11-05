using System.Text.Json.Serialization;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.TwoFactor;

[JsonConverter(typeof(JsonStringEnumConverter<TwoFactorMethod>))]
public enum TwoFactorMethod
{
    Totp,
    Passkey,
    Recovery
}

[JsonConverter(typeof(JsonStringEnumConverter<TwoFactorStage>))]
public enum TwoFactorStage
{
    Verify,
    Setup,
    Profile
}

public sealed record TwoFactorTicket(
    Guid UserId,
    TwoFactorStage Stage,
    DateTimeOffset Expires,
    MagicLinkFunction? Function = null,
    string? Ceremony = null);

public sealed record TwoFactorChallengeResponse(
    string Ticket,
    IReadOnlyList<TwoFactorMethod> Methods,
    bool SetupRequired,
    MagicLinkFunction? Function = null);

public sealed record TotpSetup(string Secret, string Uri, string QrCode);

public sealed record Enrolled(IReadOnlyList<string> RecoveryCodes);

public sealed record RecoveryOutcome(IReadOnlyList<string> NewCodes);

public sealed record PasskeyRegistrationChallenge(string Ticket, CredentialCreateOptions Options);

public sealed record PasskeyAssertionChallenge(string Ticket, AssertionOptions Options);

public sealed record TwoFactorFailure(string Code)
{
    public const string TicketInvalid = "ticketInvalid";
    public const string InvalidCode = "invalidCode";
    public const string AlreadyEnabled = "alreadyEnabled";
    public const string NotEnabled = "notEnabled";
    public const string Required = "required";
}

public enum TwoFactorRemoval
{
    Removed,
    NotFound,
    Required
}
