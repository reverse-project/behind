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
    Option<MagicLinkSettings> MagicLink
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

public sealed record JwtSettings(string Issuer, string Audience, string SigningKey);

public sealed record MagicLinkSettings(Uuid ActualSecret, Option<Uuid> OldSecret);
