using System.ComponentModel;
using System.ComponentModel.Design;
using System.Text.Json;
using Behind.Core.Common.Configuration;
using Behind.Core.Common.Converters;

namespace Behind.Tests.CoreCommon;

public class EmailAuthenticationTypeConverterTests
{
    private readonly EmailAuthenticationTypeConverter _converter = new();

    [Fact]
    public void CanConvertFrom_String_ReturnsTrue()
    {
        Assert.True(_converter.CanConvertFrom(typeof(string)));
    }

    [Fact]
    public void CanConvertFrom_OtherType_ReturnsFalse()
    {
        Assert.False(_converter.CanConvertFrom(typeof(int)));
    }

    [Fact]
    public void ConvertFrom_NoString_ReturnsNoAuthentication()
    {
        var result = _converter.ConvertFrom("No");
        
        Assert.NotNull(result);
        Assert.IsType<NoAuthentication>(result);
    }

    [Fact]
    public void ConvertFrom_BasicString_WithoutContext_ReturnsBasicAuthenticationWithEmptyCredentials()
    {
        var result = _converter.ConvertFrom("Basic");
        
        Assert.NotNull(result);
        var basicAuth = Assert.IsType<BasicAuthentication>(result);
        Assert.Equal("", basicAuth.Username);
        Assert.Equal("", basicAuth.Password);
    }

    [Fact]
    public void ConvertFrom_BasicString_WithBasicAuthenticationContext_PreservesCredentials()
    {
        var context = new BasicAuthentication("testuser", "testpass");
        var descriptor = TypeDescriptor.GetProperties(context)["Username"];
        var typeDescriptorContext = new TestTypeDescriptorContext(context);

        var result = _converter.ConvertFrom(typeDescriptorContext, null, "Basic");

        Assert.NotNull(result);
        var basicAuth = Assert.IsType<BasicAuthentication>(result);
        Assert.Equal("testuser", basicAuth.Username);
        Assert.Equal("testpass", basicAuth.Password);
    }

    [Fact]
    public void ConvertFrom_InvalidString_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => _converter.ConvertFrom("Invalid"));
        Assert.Equal("Invalid authentication type (Parameter 'value')", exception.Message);
    }

    [Fact]
    public void ConvertFrom_NonString_DelegatesToBase()
    {
        var exception = Assert.Throws<NotSupportedException>(() => _converter.ConvertFrom(123));
        Assert.Contains("cannot convert from", exception.Message);
    }

    private class TestTypeDescriptorContext : ITypeDescriptorContext
    {
        private readonly object _instance;

        public TestTypeDescriptorContext(object instance)
        {
            _instance = instance;
        }

        public object Instance => _instance;
        public PropertyDescriptor PropertyDescriptor => null!;
        public IContainer Container => null!;

        public object? GetService(Type serviceType) => null;
        public void OnComponentChanged() { }
        public bool OnComponentChanging() => true;
    }
}

public class EmailAuthenticationJsonConverterTests
{
    private readonly JsonSerializerOptions _options;

    public EmailAuthenticationJsonConverterTests()
    {
        _options = new JsonSerializerOptions
        {
            Converters = { new EmailAuthenticationJsonConverter() }
        };
    }

    [Fact]
    public void Read_NoAuthenticationString_ReturnsNoAuthentication()
    {
        var json = "\"No\"";
        var result = JsonSerializer.Deserialize<Authentication>(json, _options);

        Assert.NotNull(result);
        Assert.IsType<NoAuthentication>(result);
    }

    [Fact]
    public void Read_NonStringToken_ReturnsNull()
    {
        var json = "123";
        var result = JsonSerializer.Deserialize<Authentication>(json, _options);

        Assert.Null(result);
    }

    [Fact]
    public void Read_NullValue_ReturnsNull()
    {
        var json = "null";
        var result = JsonSerializer.Deserialize<Authentication>(json, _options);

        Assert.Null(result);
    }

    [Fact]
    public void Read_BasicAuthenticationObject_KeepsTheCredentials()
    {
        var result = JsonSerializer.Deserialize<Authentication>(
            """{ "Type": "Basic", "Username": "user", "Password": "pass" }""",
            _options);

        var basic = Assert.IsType<BasicAuthentication>(result);
        Assert.Equal(("user", "pass"), (basic.Username, basic.Password));
    }

    [Fact]
    public void Read_NoAuthenticationObject_ReturnsNoAuthentication()
    {
        Assert.IsType<NoAuthentication>(JsonSerializer.Deserialize<Authentication>("""{ "Type": "No" }""", _options));
    }

    [Fact]
    public void Read_ObjectWithoutAType_ReturnsNull()
    {
        Assert.Null(JsonSerializer.Deserialize<Authentication>("""{ "Username": "user" }""", _options));
    }

    [Fact]
    public void Write_BasicAuthentication_WritesAnObjectThatReadsBack()
    {
        var json = JsonSerializer.Serialize<Authentication>(new BasicAuthentication("user", "pass"), _options);

        Assert.Equal("""{"Type":"Basic","Username":"user","Password":"pass"}""", json);
        Assert.Equal(new BasicAuthentication("user", "pass"), JsonSerializer.Deserialize<Authentication>(json, _options));
    }

    [Fact]
    public void Write_NoAuthentication_WritesOnlyTheType()
    {
        Assert.Equal("""{"Type":"No"}""", JsonSerializer.Serialize<Authentication>(new NoAuthentication(), _options));
    }
}
