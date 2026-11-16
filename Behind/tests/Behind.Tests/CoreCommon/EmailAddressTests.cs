using System.ComponentModel;
using System.Text.Json;
using Behind.Core.Common.Converters;
using Behind.Core.Common.Email;

namespace Behind.Tests.CoreCommon;

public sealed class EmailAddressTests
{
    [Fact]
    public void Create_ValidAddress_SetsAddressAndToString()
    {
        var emailAddress = new EmailAddress { Address = "user@example.com" };

        Assert.Equal("user@example.com", emailAddress.Address);
        Assert.Equal("user@example.com", emailAddress.ToString());
    }

    [Fact]
    public void Create_AddressWithDisplayName_StoresAddressOnly()
    {
        var emailAddress = new EmailAddress { Address = "User <user@example.com>" };

        Assert.Equal("user@example.com", emailAddress.Address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email-address")]
    public void Create_InvalidAddress_ThrowsArgumentException(string address)
        => Assert.Throws<ArgumentException>(() => new EmailAddress { Address = address });
}

public sealed class EmailAddressTypeConverterTests
{
    private static readonly EmailAddressTypeConverter Converter = new();

    [Fact]
    public void TypeDescriptor_UsesEmailAddressConverter()
        => Assert.IsType<EmailAddressTypeConverter>(TypeDescriptor.GetConverter(typeof(EmailAddress)));

    [Fact]
    public void CanConvertFrom_String_ReturnsTrue()
        => Assert.True(Converter.CanConvertFrom(null, typeof(string)));

    [Fact]
    public void CanConvertFrom_Int_ReturnsFalse()
        => Assert.False(Converter.CanConvertFrom(null, typeof(int)));

    [Fact]
    public void ConvertFrom_ValidString_ReturnsEmailAddress()
    {
        var result = Converter.ConvertFromInvariantString("user@example.com");

        var emailAddress = Assert.IsType<EmailAddress>(result);
        Assert.Equal("user@example.com", emailAddress.Address);
    }

    [Fact]
    public void ConvertFrom_InvalidString_ThrowsArgumentException()
        => Assert.Throws<ArgumentException>(() => Converter.ConvertFromInvariantString("invalid"));
}

public sealed class EmailAddressJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new();

    static EmailAddressJsonConverterTests()
        => Options.Converters.Add(new EmailAddressJsonConverter());

    [Fact]
    public void Write_EmailAddress_SerializesAddressAsString()
    {
        var emailAddress = new EmailAddress { Address = "user@example.com" };

        Assert.Equal("\"user@example.com\"", JsonSerializer.Serialize(emailAddress, Options));
    }

    [Fact]
    public void Read_String_ReturnsEmailAddress()
    {
        var result = JsonSerializer.Deserialize<EmailAddress>("\"user@example.com\"", Options);

        var emailAddress = Assert.IsType<EmailAddress>(result);
        Assert.Equal("user@example.com", emailAddress.Address);
    }

    [Fact]
    public void Read_Null_ReturnsNull()
        => Assert.Null(JsonSerializer.Deserialize<EmailAddress>("null", Options));

    [Fact]
    public void Read_NonStringToken_ReturnsNull()
        => Assert.Null(JsonSerializer.Deserialize<EmailAddress>("42", Options));

    [Fact]
    public void Read_InvalidString_ThrowsArgumentException()
        => Assert.Throws<ArgumentException>(() =>
            JsonSerializer.Deserialize<EmailAddress>("\"invalid\"", Options));

    [Fact]
    public void Attribute_RegistersConverterForSerialization()
    {
        var emailAddress = new EmailAddress { Address = "user@example.com" };

        Assert.Equal("\"user@example.com\"", JsonSerializer.Serialize(emailAddress));
    }
}