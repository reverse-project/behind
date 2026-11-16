using System.Text;
using System.Text.Json;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Common.Converters;

namespace Behind.Tests.CoreCommon;

// =========================================================================
// PasswordHash
// =========================================================================

public class PasswordHashTests
{
    [Fact]
    public void Create_HashesPassword_VerifyReturnsTrue()
    {
        var hash = PasswordHash.Create("secret123");
        Assert.True(hash.Verify("secret123"));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = PasswordHash.Create("correct");
        Assert.False(hash.Verify("wrong"));
    }

    [Fact]
    public void CreateFromExisting_RoundTripsAsString()
    {
        var original = PasswordHash.Create("pw");
        string hashStr = original;                        // implicit operator
        var restored = PasswordHash.CreateFromExisting(hashStr);
        Assert.True(restored.Verify("pw"));
    }

    [Fact]
    public void ImplicitToString_EqualsToString()
    {
        var hash = PasswordHash.Create("abc");
        string via_op = hash;
        Assert.Equal(hash.ToString(), via_op);
    }

    [Fact]
    public void ImplicitFromString_CreatesFromExisting()
    {
        var original = PasswordHash.Create("xyz");
        string raw = original;
        PasswordHash restored = raw;                      // implicit operator from string
        Assert.True(restored.Verify("xyz"));
    }
}

// =========================================================================
// SecretKeyTypeConverter
// =========================================================================

public class SecretKeyTypeConverterTests
{
    private static readonly SecretKeyTypeConverter _converter = new();

    // The standard Base32 alphabet used by OtpNet:
    private const string ValidBase32 = "JBSWY3DPEHPK3PXP";   // "Hello World!" encoded
    private static readonly string ValidUuid = Guid.NewGuid().ToString();

    [Fact]
    public void CanConvertFrom_String_ReturnsTrue()
        => Assert.True(_converter.CanConvertFrom(null, typeof(string)));

    [Fact]
    public void CanConvertFrom_Int_ReturnsFalse()
        => Assert.False(_converter.CanConvertFrom(null, typeof(int)));

    [Fact]
    public void ConvertFrom_ValidTotp_ReturnsTotpInstance()
    {
        var result = _converter.ConvertFrom($"totp://{ValidBase32}");
        var totp = Assert.IsType<Totp>(result);
        Assert.Equal(ValidBase32, totp.Value);
    }

    [Fact]
    public void ConvertFrom_ValidUuid_ReturnsUuidInstance()
    {
        var result = _converter.ConvertFrom($"uuid://{ValidUuid}");
        var uuid = Assert.IsType<Uuid>(result);
        Assert.Equal(Guid.Parse(ValidUuid), uuid.Value);
    }

    [Theory]
    [InlineData("noSeparator")]
    [InlineData("")]
    public void ConvertFrom_MissingColon_ThrowsFormatException(string input)
        => Assert.Throws<FormatException>(() => _converter.ConvertFrom(input));

    [Fact]
    public void ConvertFrom_InvalidTotpValue_ThrowsFormatException()
        => Assert.Throws<FormatException>(() => _converter.ConvertFrom("totp://!!!NOT_BASE32!!!"));

    [Fact]
    public void ConvertFrom_InvalidUuidValue_ThrowsFormatException()
        => Assert.Throws<FormatException>(() => _converter.ConvertFrom("uuid://not-a-guid"));

    [Fact]
    public void ConvertFrom_UnknownType_ThrowsFormatException()
        => Assert.Throws<FormatException>(() => _converter.ConvertFrom("unknown://value"));
}

// =========================================================================
// SecretKeyJsonConverter
// =========================================================================

public class SecretKeyJsonConverterTests
{
    private const string ValidBase32 = "JBSWY3DPEHPK3PXP";

    private static SecretKey? Deserialize(string json)
        => JsonSerializer.Deserialize<SecretKey>(json,
            new JsonSerializerOptions { Converters = { new SecretKeyJsonConverter() } });

    private static string Serialize(SecretKey key)
        => JsonSerializer.Serialize(key,
            new JsonSerializerOptions { Converters = { new SecretKeyJsonConverter() } });

    // ------- Read -------

    [Fact]
    public void Read_NullToken_ReturnsNull()
        => Assert.Null(Deserialize("null"));

    [Fact]
    public void Read_NonStringToken_ThrowsJsonException()
        => Assert.Throws<JsonException>(() => Deserialize("42"));

    [Fact]
    public void Read_EmptyString_ThrowsJsonException()
        => Assert.Throws<JsonException>(() => Deserialize("\"\""));

    [Fact]
    public void Read_WhiteSpaceString_ThrowsJsonException()
        => Assert.Throws<JsonException>(() => Deserialize("\"   \""));

    [Fact]
    public void Read_ValidTotpString_ReturnsTotpInstance()
    {
        var result = Deserialize($"\"totp://{ValidBase32}\"");
        Assert.IsType<Totp>(result);
    }

    [Fact]
    public void Read_ValidUuidString_ReturnsUuidInstance()
    {
        var guid = Guid.NewGuid();
        var result = Deserialize($"\"uuid://{guid}\"");
        var uuid = Assert.IsType<Uuid>(result);
        Assert.Equal(guid, uuid.Value);
    }

    [Fact]
    public void Read_InvalidFormat_ThrowsJsonException()
        => Assert.Throws<JsonException>(() => Deserialize("\"bad_format\""));

    // ------- Write -------

    [Fact]
    public void Write_Totp_SerializesToTotpScheme()
    {
        if (!Totp.TryCreate(ValidBase32, out var totp))
        {
            Assert.Fail("Could not create Totp for test.");
        }

        var json = Serialize(totp);
        Assert.Equal($"\"totp://{ValidBase32}\"", json);
    }

    [Fact]
    public void Write_Uuid_SerializesToUuidScheme()
    {
        var guid = Guid.NewGuid();
        if (!Uuid.TryCreate(guid.ToString(), out var uuid))
        {
            Assert.Fail("Could not create Uuid for test.");
        }

        var json = Serialize(uuid);
        Assert.Equal($"\"uuid://{guid}\"", json);
    }
}

// =========================================================================
// Totp
// =========================================================================

public class TotpTests
{
    private const string ValidBase32 = "JBSWY3DPEHPK3PXP";

    [Fact]
    public void TryCreate_ValidBase32_ReturnsTrueAndValue()
    {
        var success = Totp.TryCreate(ValidBase32, out var result);
        Assert.True(success);
        Assert.Equal(ValidBase32, result.Value);
    }

    [Fact]
    public void TryCreate_PaddedBase32_StoresTheValueWithoutPadding()
    {
        var success = Totp.TryCreate("JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXPJBSWY3DPEHPK====", out var result);

        Assert.True(success);
        Assert.Equal("JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXPJBSWY3DPEHPK", result.Value);
    }

    [Fact]
    public void TryCreate_InvalidBase32_ReturnsFalse()
    {
        var success = Totp.TryCreate("!!! NOT BASE32 !!!", out var result);
        Assert.False(success);
        Assert.Equal(Totp.Invalid, result);
    }

    [Fact]
    public void Invalid_StaticInstance_HasInvalidName()
        => Assert.Equal("Invalid", Totp.Invalid.Value);
}

// =========================================================================
// Uuid
// =========================================================================

public class UuidTests
{
    [Fact]
    public void TryCreate_ValidGuid_ReturnsTrueAndValue()
    {
        var guid = Guid.NewGuid();
        var success = Uuid.TryCreate(guid.ToString(), out var result);
        Assert.True(success);
        Assert.Equal(guid, result.Value);
    }

    [Fact]
    public void TryCreate_InvalidGuid_ReturnsFalse()
    {
        var success = Uuid.TryCreate("not-a-guid", out var result);
        Assert.False(success);
        Assert.Equal(Uuid.Empty, result);
    }

    [Fact]
    public void Empty_StaticInstance_HasEmptyGuid()
        => Assert.Equal(Guid.Empty, Uuid.Empty.Value);
}

// =========================================================================
// ConfigurationForbiddenResponse
// =========================================================================

public class ConfigurationForbiddenResponseTests
{
    [Fact]
    public void InvalidTotp_HasCorrectCode()
        => Assert.Equal("invalid_totp", ConfigurationForbiddenResponse.InvalidTotp.Code);

    [Fact]
    public void InvalidTotp_HasNonEmptyMessage()
        => Assert.NotEmpty(ConfigurationForbiddenResponse.InvalidTotp.Message);

    [Fact]
    public void ConfigurationReadOnly_HasCorrectCode()
        => Assert.Equal("configuration_read_only", ConfigurationForbiddenResponse.ConfigurationReadOnly.Code);

    [Fact]
    public void ConfigurationReadOnly_HasNonEmptyMessage()
        => Assert.NotEmpty(ConfigurationForbiddenResponse.ConfigurationReadOnly.Message);
}

// =========================================================================
// SecurityConfiguration + FrontendConfiguration
// =========================================================================

public class ConfigurationRecordTests
{
    [Fact]
    public void SecurityConfiguration_DefaultCtor_ReadOnlyIsFalse()
    {
        var cfg = new SecurityConfiguration();
        Assert.False(cfg.ReadOnlyConfiguration);
    }

    [Fact]
    public void SecurityConfiguration_SectionName_IsCorrect()
        => Assert.Equal("Security", SecurityConfiguration.SectionName);

    [Fact]
    public void FrontendConfiguration_SectionName_IsCorrect()
        => Assert.Equal("Frontend", FrontendConfiguration.SectionName);

    [Fact]
    public void JwtSettings_Constructor_SetsProperties()
    {
        var jwt = new JwtSettings("issuer", "audience", "key");
        Assert.Equal("issuer", jwt.Issuer);
        Assert.Equal("audience", jwt.Audience);
        Assert.Equal("key", jwt.SigningKey);
    }
}
