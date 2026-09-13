using System.ComponentModel;
using System.ComponentModel.Design;
using System.Text.Json;
using Behind.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Common.Converters;

namespace Behind.Tests.CoreCommon;

public class EmailConfigurationTypeConverterTests
{
    private readonly EmailConfigurationTypeConverter _converter = new();

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
    public void ConvertFrom_SmtpString_WithoutContext_ReturnsSmtpEmailConfigurationWithDefaults()
    {
        var result = _converter.ConvertFrom("Smtp");

        Assert.NotNull(result);
        var smtpConfig = Assert.IsType<SmtpEmailConfiguration>(result);
        Assert.Equal("", smtpConfig.SmtpServer);
        Assert.Equal(0, smtpConfig.Port);
        Assert.False(smtpConfig.Ssl.Enabled);
        var auth = smtpConfig.Authentication.Match(
            () => null as Authentication,
            a => a
        );
        Assert.NotNull(auth);
        Assert.IsType<NoAuthentication>(auth);
    }

    [Fact]
    public void ConvertFrom_SmtpString_WithSmtpConfigContext_PreservesExistingConfiguration()
    {
        var existingConfig = new SmtpEmailConfiguration(
            "smtp.example.com",
            587,
            new Ssl(true),
            new BasicAuthentication("user", "pass")
        );

        var typeDescriptorContext = new TestTypeDescriptorContext(existingConfig);
        var result = _converter.ConvertFrom(typeDescriptorContext, null, "Smtp");

        Assert.NotNull(result);
        var smtpConfig = Assert.IsType<SmtpEmailConfiguration>(result);
        Assert.Equal("smtp.example.com", smtpConfig.SmtpServer);
        Assert.Equal(587, smtpConfig.Port);
        Assert.True(smtpConfig.Ssl.Enabled);
        var basicAuth = smtpConfig.Authentication.Match(
            () => null as BasicAuthentication,
            auth => auth as BasicAuthentication
        );
        Assert.NotNull(basicAuth);
        Assert.Equal("user", basicAuth.Username);
        Assert.Equal("pass", basicAuth.Password);
    }

    [Fact]
    public void ConvertFrom_LoggerString_ReturnsLoggerEmailConfiguration()
    {
        var result = _converter.ConvertFrom("Logger");

        Assert.NotNull(result);
        Assert.IsType<LoggerEmailConfiguration>(result);
    }

    [Fact]
    public void ConvertFrom_InvalidString_ThrowsNotSupportedException()
    {
        var exception = Assert.Throws<NotSupportedException>(() => _converter.ConvertFrom("Invalid"));
        Assert.Contains("Unsupported email configuration type", exception.Message);
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

public class EmailConfigurationJsonConverterTests
{
    private readonly JsonSerializerOptions _options;

    public EmailConfigurationJsonConverterTests()
    {
        _options = new JsonSerializerOptions
        {
            Converters = { new EmailConfigurationJsonConverter(), new EmailAuthenticationJsonConverter() },
            WriteIndented = true
        };
    }

    [Fact]
    public void Read_LoggerConfiguration_ReturnsLoggerEmailConfiguration()
    {
        var json = @"{""Provider"": ""Logger""}";
        var result = JsonSerializer.Deserialize<EmailConfiguration>(json, _options);

        Assert.NotNull(result);
        Assert.IsType<LoggerEmailConfiguration>(result);
    }

    [Fact]
    public void Read_SmtpConfiguration_ReturnsSmtpEmailConfiguration()
    {
        var json = @"{
  ""Provider"": ""Smtp"",
  ""SmtpServer"": ""smtp.gmail.com"",
  ""Port"": 587,
  ""Ssl"": {
    ""Enabled"": true
  },
  ""Authentication"": ""No""
}";
        var result = JsonSerializer.Deserialize<EmailConfiguration>(json, _options);

        Assert.NotNull(result);
        var smtpConfig = Assert.IsType<SmtpEmailConfiguration>(result);
        Assert.Equal("smtp.gmail.com", smtpConfig.SmtpServer);
        Assert.Equal(587, smtpConfig.Port);
        Assert.True(smtpConfig.Ssl.Enabled);
        var auth = smtpConfig.Authentication.Match(
            () => null as Authentication,
            a => a
        );
        Assert.NotNull(auth);
        Assert.IsType<NoAuthentication>(auth);
    }

    [Fact]
    public void Read_InvalidConfigurationType_ThrowsNotSupportedException()
    {
        var json = @"{""Provider"": ""Unknown""}";
        var exception = Assert.Throws<NotSupportedException>(() =>
            JsonSerializer.Deserialize<EmailConfiguration>(json, _options));
        Assert.Contains("Unsupported email configuration type", exception.Message);
    }

    [Fact]
    public void Read_NonObjectToken_ThrowsJsonException()
    {
        var json = "\"not an object\"";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EmailConfiguration>(json, _options));
    }
}
