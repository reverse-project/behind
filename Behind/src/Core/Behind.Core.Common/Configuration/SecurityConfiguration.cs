using System.ComponentModel;
using System.Text.Json.Serialization;
using Behind.Common;
using Behind.Core.Common.Converters;
using static Behind.Common.FunctionalExtensions;
using OtpNet;

namespace Behind.Core.Common.Configuration;

public record SecurityConfiguration(
    bool ReadOnlyConfiguration,
    Option<SecretKey> SecretKey,
    Option<JwtSettings> Jwt,
    Option<MagicLinkSettings> MagicLink,
    Option<DataProtectionSettings> DataProtection = default
)
{
    public SecurityConfiguration() : this(false, None, None, None)
    {
        
    }
    
    public const string SectionName = "Security";
}

[TypeConverter(typeof(SecretKeyTypeConverter))]
[JsonConverter(typeof(SecretKeyJsonConverter))]
public abstract record SecretKey;

public sealed record Totp : SecretKey
{
    public static readonly Totp Invalid = new(nameof(Invalid));
    
    public string Value { get; init; }
    
    private Totp(string value) => Value = value;

    public static bool TryCreate(string value, out Totp result)
    {
        try
        {
            Base32Encoding.ToBytes(value);
            result = new Totp(value);
            return true;
        }
        catch
        {
            result = Invalid;
            return false;
        }
    }
}

public sealed record Uuid : SecretKey
{
    public static readonly Uuid Empty = new(Guid.Empty);
    
    public Guid Value { get; init; }
    
    private Uuid(Guid value) => Value = value;

    public static Uuid CreateVersion7(DateTimeOffset timestamp) => new(Guid.CreateVersion7(timestamp));

    public string ToConfigurationValue() => $"uuid://{Value}";

    public Option<DateTimeOffset> CreatedAt => Value.Version == 7 ? Some(Value.GetTimestamp()) : None;

    public static bool TryCreate(string value, out Uuid result)
    {
        if (Guid.TryParse(value, out var guid))
        {
            result = new Uuid(guid);
            return true;
        }

        result = Empty;
        return false;
    }
}

public sealed record JwtSettings(string Issuer, string Audience, [property: Sensible] string SigningKey);

public sealed record MagicLinkSettings(
    Uuid ActualSecret,
    Option<Uuid> OldSecret,
    Option<TimeSpan> RotationInterval = default,
    Option<TimeSpan> RotationOverlap = default,
    Option<TimeSpan> LinkLifetime = default,
    Option<TimeSpan> RequestCooldown = default,
    Option<int> RequestLimit = default,
    Option<TimeSpan> RequestWindow = default,
    Option<int> RedeemLimit = default,
    Option<TimeSpan> RedeemWindow = default
)
{
    public static readonly TimeSpan DefaultRotationInterval = TimeSpan.FromDays(30);
    public static readonly TimeSpan DefaultRotationOverlap = TimeSpan.FromDays(1);
    public static readonly TimeSpan DefaultLinkLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DefaultRequestCooldown = TimeSpan.FromMinutes(1);
    public const int DefaultRequestLimit = 5;
    public static readonly TimeSpan DefaultRequestWindow = TimeSpan.FromMinutes(10);
    public const int DefaultRedeemLimit = 10;
    public static readonly TimeSpan DefaultRedeemWindow = TimeSpan.FromMinutes(10);
}

public sealed record DataProtectionSettings(
    string KeysPath,
    string CertificatePath,
    [property: Sensible] string CertificatePassword
);
