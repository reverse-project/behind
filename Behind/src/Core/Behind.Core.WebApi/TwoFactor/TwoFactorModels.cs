using System.Text.Json.Serialization;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.TwoFactor;

[JsonConverter(typeof(JsonStringEnumConverter<TwoFactorMethod>))]
public enum TwoFactorMethod
{
    Totp,
    Passkey,
    Recovery,
    Email
}

[JsonConverter(typeof(JsonStringEnumConverter<TwoFactorStage>))]
public enum TwoFactorStage
{
    Verify,
    Setup,
    Profile,
    PrimaryPasskey
}

public sealed record TwoFactorTicket(
    Guid UserId,
    TwoFactorStage Stage,
    DateTimeOffset Expires,
    MagicLinkFunction? Function = null,
    string? Ceremony = null,
    string? PrimaryCredentialId = null,
    Guid? Nonce = null,
    string? Email = null);

public sealed record TwoFactorChallengeResponse(
    string Ticket,
    IReadOnlyList<TwoFactorMethod> Methods,
    bool SetupRequired,
    MagicLinkFunction? Function = null)
{
    [JsonIgnore]
    public bool Unavailable { get; init; }
}

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
    public const string Unavailable = "twoFactorUnavailable";
}

public enum TwoFactorRemoval
{
    Removed,
    NotFound,
    Required
}
