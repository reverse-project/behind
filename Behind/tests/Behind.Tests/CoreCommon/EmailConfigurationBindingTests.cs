using Behind.Common.Configuration;
using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreCommon;

public class EmailConfigurationBindingTests
{
    private static EmailConfiguration? Bind(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => $"Email:{pair.Key}", pair => (string?)pair.Value))
            .Build()
            .GetSection(EmailConfiguration.SectionName)
            .GetOptionAware<EmailConfiguration>();

    private static SmtpEmailConfiguration Smtp(params (string Key, string Value)[] extra) =>
        Assert.IsType<SmtpEmailConfiguration>(
            Bind([("Provider", "Smtp"), ("SmtpServer", "smtp.example.com"), ("Port", "587"), ..extra]));

    private static Authentication AuthenticationOf(SmtpEmailConfiguration smtp) =>
        smtp.Authentication.Match(() => throw new InvalidOperationException("No authentication."), authentication => authentication);

    [Fact]
    public void Bind_SmtpProvider_ReturnsTheSmtpRecord()
    {
        var smtp = Smtp(("Ssl:Enabled", "true"), ("From", "noreply@example.com"));

        Assert.Equal("smtp.example.com", smtp.SmtpServer);
        Assert.Equal(587, smtp.Port);
        Assert.True(smtp.Ssl.Enabled);
        Assert.Equal(Some("noreply@example.com"), smtp.From);
    }

    [Fact]
    public void Bind_LoggerProvider_ReturnsTheLoggerRecord()
    {
        Assert.IsType<LoggerEmailConfiguration>(Bind(("Provider", "Logger")));
    }

    [Fact]
    public void Bind_WithoutAProvider_ReturnsNothing()
    {
        Assert.Null(Bind(("SmtpServer", "smtp.example.com")));
    }

    [Fact]
    public void Bind_UnknownProvider_Throws()
    {
        Assert.Throws<NotSupportedException>(() => Bind(("Provider", "Carrier pigeon")));
    }

    [Fact]
    public void Bind_BasicAuthenticationObject_KeepsTheCredentials()
    {
        var basic = Assert.IsType<BasicAuthentication>(AuthenticationOf(Smtp(
            ("Authentication:Type", "Basic"),
            ("Authentication:Username", "user@example.com"),
            ("Authentication:Password", "secret"))));

        Assert.Equal(("user@example.com", "secret"), (basic.Username, basic.Password));
    }

    [Fact]
    public void Bind_NoAuthenticationObject_ReturnsNoAuthentication()
    {
        Assert.IsType<NoAuthentication>(AuthenticationOf(Smtp(("Authentication:Type", "No"))));
    }

    [Fact]
    public void Bind_AuthenticationText_StillWorks()
    {
        Assert.IsType<NoAuthentication>(AuthenticationOf(Smtp(("Authentication", "No"))));
    }

    [Fact]
    public void Bind_AuthenticationWithoutAType_IsNone()
    {
        Assert.Equal(None, Smtp(("Authentication:Username", "user")).Authentication);
    }

    [Fact]
    public void Bind_Templates_KeepsTheNamesAndContents()
    {
        var templates = Smtp(("Templates:ConfirmationEmail", "<b>hi</b>"), ("Templates:MagicLinkEmail", "link"))
            .Templates.Match(() => throw new InvalidOperationException("No templates."), found => found.ToList());

        Assert.Equal(
            [("ConfirmationEmail", "<b>hi</b>"), ("MagicLinkEmail", "link")],
            templates.Select(template => (template.Name, template.Content)));
    }

    [Fact]
    public void Bind_WithoutTemplates_IsNone()
    {
        Assert.Equal(None, Smtp().Templates);
    }
}
