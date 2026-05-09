using System.ComponentModel;
using System.Text.Json.Serialization;
using Behind.Core.Common.Converters;
using OtpNet;

namespace Behind.Core.Common.Configuration;

public record SecurityConfiguration(bool ReadOnlyConfiguration, SecretKey? SecretKey)
{
    public SecurityConfiguration() : this(false, null)
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