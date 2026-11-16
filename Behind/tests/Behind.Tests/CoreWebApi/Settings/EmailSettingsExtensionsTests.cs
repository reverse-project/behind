using System.Text.Json.Nodes;
using Behind.Common.Templating;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Settings;

namespace Behind.Tests.CoreWebApi.Settings;

public sealed class EmailSettingsExtensionsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, true);

    private FileInfo FileWith(string content)
    {
        var path = Path.Combine(_directory, "email.json");
        File.WriteAllText(path, content);
        return new FileInfo(path);
    }

    private static SmtpSettingsRequest SmtpRequest(
        string authentication = "No",
        string? username = null,
        string? password = null,
        string? from = null,
        IReadOnlyDictionary<string, string>? templates = null,
        string server = "smtp.example.com",
        int port = 587) =>
        new(server, port, true, from, authentication, username, password, templates);

    private static Option<EmailConfiguration> Current(string? password = "stored") =>
        Some<EmailConfiguration>(new SmtpEmailConfiguration(
            "old.example.com",
            25,
            new Ssl(false),
            password is null ? Some<Authentication>(new NoAuthentication()) : Some<Authentication>(new BasicAuthentication("old-user", password)),
            None));

    private static SmtpEmailConfiguration Applied(Option<EmailConfiguration> current, SmtpSettingsRequest smtp) =>
        Assert.IsType<SmtpEmailConfiguration>(
            current.Apply(new EmailSettingsRequest("Smtp", smtp)).Match(
                () => throw new InvalidOperationException("The request was refused."),
                configuration => configuration));

    [Fact]
    public void ToSettings_WithoutAConfiguration_OffersTheLoggerAndSmtpDefaults()
    {
        var settings = default(Option<EmailConfiguration>).ToSettings();

        Assert.Equal("Logger", settings.Provider);
        Assert.Equal(["Logger", "Smtp"], settings.Providers);
        Assert.Equal(["No", "Basic"], settings.AuthenticationTypes);
        Assert.Equal(SmtpSettingsResponse.Default, settings.Smtp);
        Assert.Equal(
            ["ConfirmationEmail", "PasswordResetCodeEmail", "PasswordResetLinkEmail", "MagicLinkEmail"],
            settings.DefaultTemplates.Keys);
    }

    [Fact]
    public void ToSettings_ForSmtp_MasksThePassword()
    {
        var settings = Current("super-secret-password").ToSettings();

        Assert.Equal("Smtp", settings.Provider);
        Assert.Equal("old.example.com", settings.Smtp.Server);
        Assert.Equal(25, settings.Smtp.Port);
        Assert.False(settings.Smtp.Ssl);
        Assert.Equal("Basic", settings.Smtp.Authentication);
        Assert.Equal("old-user", settings.Smtp.Username);
        Assert.Equal("super-secret-password".MaskSensitiveValue(), settings.Smtp.PasswordMask);
        Assert.DoesNotContain("super-secret-password", System.Text.Json.JsonSerializer.Serialize(settings));
    }

    [Fact]
    public void ToSettings_ForLogger_FallsBackToTheSmtpDefaults()
    {
        var settings = Some<EmailConfiguration>(new LoggerEmailConfiguration()).ToSettings();

        Assert.Equal("Logger", settings.Provider);
        Assert.Equal(SmtpSettingsResponse.Default, settings.Smtp);
    }

    [Fact]
    public void Apply_Logger_CreatesTheLoggerRecord()
    {
        var result = Current().Apply(new EmailSettingsRequest("Logger", null));

        Assert.Equal(Some<EmailConfiguration>(new LoggerEmailConfiguration()), result);
    }

    [Fact]
    public void Apply_Smtp_CreatesTheSmtpRecord()
    {
        var smtp = Applied(
            default,
            SmtpRequest(
                "Basic",
                " user@example.com ",
                "pass",
                " noreply@example.com ",
                new Dictionary<string, string> { ["MagicLinkEmail"] = "<p>x</p>", ["ConfirmationEmail"] = " " }));

        Assert.Equal(("smtp.example.com", 587, true), (smtp.SmtpServer, smtp.Port, smtp.Ssl.Enabled));
        Assert.Equal(Some("noreply@example.com"), smtp.From);
        Assert.Equal(Some<Authentication>(new BasicAuthentication("user@example.com", "pass")), smtp.Authentication);
        Assert.Equal(
            [("MagicLinkEmail", "<p>x</p>")],
            smtp.Templates.Match(() => [], templates => templates.Select(template => (template.Name, template.Content)).ToList()));
    }

    [Fact]
    public void Apply_NoAuthenticationAndNoOptionalValues_LeavesThemNone()
    {
        var smtp = Applied(default, SmtpRequest(from: " "));

        Assert.Equal(Some<Authentication>(new NoAuthentication()), smtp.Authentication);
        Assert.Equal(None, smtp.From);
        Assert.Equal(None, smtp.Templates);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Apply_BlankPassword_KeepsTheStoredOne(string? password)
    {
        var smtp = Applied(Current("stored"), SmtpRequest("Basic", "new-user", password));

        Assert.Equal(Some<Authentication>(new BasicAuthentication("new-user", "stored")), smtp.Authentication);
    }

    [Fact]
    public void Apply_NewPassword_ReplacesTheStoredOne()
    {
        var smtp = Applied(Current("stored"), SmtpRequest("Basic", "new-user", "replacement"));

        Assert.Equal(Some<Authentication>(new BasicAuthentication("new-user", "replacement")), smtp.Authentication);
    }

    [Fact]
    public void Apply_SwitchingFromTheLoggerToBasic_NeedsAPassword()
    {
        var current = Some<EmailConfiguration>(new LoggerEmailConfiguration());

        Assert.Equal(None, current.Apply(new EmailSettingsRequest("Smtp", SmtpRequest("Basic", "user", null))));
    }

    [Theory]
    [InlineData("Carrier pigeon")]
    [InlineData("smtp")]
    public void Apply_UnknownProvider_IsRefused(string provider)
    {
        Assert.Equal(None, Current().Apply(new EmailSettingsRequest(provider, SmtpRequest())));
    }

    [Fact]
    public void Apply_SmtpWithoutItsSettings_IsRefused()
    {
        Assert.Equal(None, Current().Apply(new EmailSettingsRequest("Smtp", null)));
    }

    public static TheoryData<SmtpSettingsRequest> InvalidRequests =>
        new()
        {
            SmtpRequest(server: " "),
            SmtpRequest(port: 0),
            SmtpRequest(port: 70000),
            SmtpRequest(from: "not an address"),
            SmtpRequest("Basic", " ", "pass"),
            SmtpRequest("Digest"),
            SmtpRequest(templates: new Dictionary<string, string> { ["Unknown"] = "x" })
        };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void Apply_InvalidSmtpSettings_AreRefused(SmtpSettingsRequest request)
    {
        Assert.Equal(None, default(Option<EmailConfiguration>).Apply(new EmailSettingsRequest("Smtp", request)));
    }

    [Fact]
    public async Task WriteEmailConfigurationAsync_ReplacesTheEmailSectionAndKeepsOtherSections()
    {
        var file = FileWith("""{ "Email": { "Provider": "Logger" }, "Other": { "Keep": true } }""");
        var smtp = Applied(
            default,
            SmtpRequest("Basic", "user", "pass", "noreply@example.com", new Dictionary<string, string> { ["MagicLinkEmail"] = "x" }));

        await file.WriteEmailConfigurationAsync(smtp);

        var root = JsonNode.Parse(File.ReadAllText(file.FullName))!;
        Assert.True((bool)root["Other"]!["Keep"]!);
        var email = root["Email"]!;
        Assert.Equal("Smtp", (string)email["Provider"]!);
        Assert.Equal("smtp.example.com", (string)email["SmtpServer"]!);
        Assert.Equal(587, (int)email["Port"]!);
        Assert.True((bool)email["Ssl"]!["Enabled"]!);
        Assert.Equal("Basic", (string)email["Authentication"]!["Type"]!);
        Assert.Equal("pass", (string)email["Authentication"]!["Password"]!);
        Assert.Equal("x", (string)email["Templates"]!["MagicLinkEmail"]!);
    }

    [Fact]
    public async Task WriteEmailConfigurationAsync_OmitsWhatIsNone()
    {
        var file = FileWith("{}");

        await file.WriteEmailConfigurationAsync(Applied(default, SmtpRequest()));

        var email = JsonNode.Parse(File.ReadAllText(file.FullName))!["Email"]!.AsObject();
        Assert.False(email.ContainsKey("From"));
        Assert.False(email.ContainsKey("Templates"));
    }

    [Fact]
    public async Task WrittenConfiguration_ReadsBackAsTheSameRecord()
    {
        var file = FileWith("{}");
        var smtp = Applied(
            default,
            SmtpRequest("Basic", "user", "pass", "noreply@example.com", new Dictionary<string, string> { ["MagicLinkEmail"] = "x" }));

        await file.WriteEmailConfigurationAsync(smtp);
        var read = Assert.IsType<SmtpEmailConfiguration>(
            file.ReadEmailConfiguration().Match(() => throw new InvalidOperationException(), configuration => configuration));

        Assert.Equal(smtp.SmtpServer, read.SmtpServer);
        Assert.Equal(smtp.Port, read.Port);
        Assert.Equal(smtp.Ssl, read.Ssl);
        Assert.Equal(smtp.From, read.From);
        Assert.Equal(smtp.Authentication, read.Authentication);
        Assert.Equal(
            smtp.Templates.Match(() => [], templates => templates.Select(template => (template.Name, template.Content)).ToList()),
            read.Templates.Match(() => [], templates => templates.Select(template => (template.Name, template.Content)).ToList()));
    }

    [Fact]
    public async Task WrittenLoggerConfiguration_ReadsBackAsTheLogger()
    {
        var file = FileWith("""{ "Email": { "Provider": "Smtp", "SmtpServer": "x", "Port": 1 } }""");

        await file.WriteEmailConfigurationAsync(new LoggerEmailConfiguration());

        Assert.Equal(Some<EmailConfiguration>(new LoggerEmailConfiguration()), file.ReadEmailConfiguration());
        Assert.Equal("""{"Provider":"Logger"}""", JsonNode.Parse(File.ReadAllText(file.FullName))!["Email"]!.ToJsonString());
    }

    [Fact]
    public void ReadEmailConfiguration_WithoutAnEmailSection_IsNone()
    {
        Assert.Equal(None, FileWith("{}").ReadEmailConfiguration());
    }
}
