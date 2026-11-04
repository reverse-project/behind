using System.Text.Json.Nodes;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class EmailSettingsEndpointsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;
    private readonly ILogger<SettingsEndpoints> _logger = NullLogger<SettingsEndpoints>.Instance;

    public EmailSettingsEndpointsTests()
    {
        File.WriteAllText(PathOf("email.json"), """{ "Email": { "Provider": "Logger" } }""");
    }

    public void Dispose() => Directory.Delete(_directory, true);

    private string PathOf(string name) => Path.Combine(_directory, name);

    private ConfigurationFileCatalog Catalog => ConfigurationFileCatalog.For(_directory);

    private static IOptions<SecurityConfiguration> Security(bool readOnly = false) =>
        Options.Create(new SecurityConfiguration(readOnly, None, None, None));

    private JsonNode Email() => JsonNode.Parse(File.ReadAllText(PathOf("email.json")))!["Email"]!;

    private static EmailSettingsRequest SmtpRequest(string? password = "pass") =>
        new("Smtp", new SmtpSettingsRequest("smtp.example.com", 587, true, "noreply@example.com", "Basic", "user", password, null));

    private Task<IResult> Update(EmailSettingsRequest request, bool readOnly = false) =>
        SettingsEndpoints.UpdateEmail(request, Catalog, Security(readOnly), _logger, CancellationToken.None);

    [Fact]
    public async Task Get_ListsTheEmailFileWithItsOwnEditorAndNoRawFields()
    {
        File.WriteAllText(PathOf("backend.json"), """{ "A": 1 }""");

        var response = Assert.IsType<JsonHttpResult<SettingsResponse>>(
            await SettingsEndpoints.Get(Catalog, TestLocales.Embedded, Security(), CancellationToken.None)).Value!;

        var email = response.Files.Single(file => file.Name == "email.json");
        Assert.Equal("email", email.Editor);
        Assert.Empty(email.Fields);
        Assert.Null(response.Files.Single(file => file.Name == "backend.json").Editor);
    }

    [Fact]
    public async Task GetEmail_ReadsTheRecordWithAMaskedPassword()
    {
        await Update(SmtpRequest("super-secret-password"));

        var result = await SettingsEndpoints.GetEmail(Catalog);

        var settings = Assert.IsType<JsonHttpResult<EmailSettingsResponse>>(result).Value!;
        Assert.Equal("Smtp", settings.Provider);
        Assert.Equal("smtp.example.com", settings.Smtp.Server);
        Assert.Equal("super-secret-password".MaskSensitiveValue(), settings.Smtp.PasswordMask);
        Assert.DoesNotContain("super-secret-password", System.Text.Json.JsonSerializer.Serialize(settings));
    }

    [Fact]
    public async Task GetEmail_WithoutTheFile_IsNotFound()
    {
        File.Delete(PathOf("email.json"));

        Assert.IsType<NotFound>(await SettingsEndpoints.GetEmail(Catalog));
    }

    [Fact]
    public async Task Update_WritesTheChosenProviderAsARecord()
    {
        var result = await Update(SmtpRequest());

        Assert.IsType<NoContent>(result);
        var email = Email();
        Assert.Equal("Smtp", (string)email["Provider"]!);
        Assert.Equal("smtp.example.com", (string)email["SmtpServer"]!);
        Assert.Equal("Basic", (string)email["Authentication"]!["Type"]!);
        Assert.Equal("pass", (string)email["Authentication"]!["Password"]!);
    }

    [Fact]
    public async Task Update_BlankPassword_KeepsTheStoredPassword()
    {
        await Update(SmtpRequest("stored"));

        await Update(SmtpRequest(password: null));

        Assert.Equal("stored", (string)Email()["Authentication"]!["Password"]!);
    }

    [Fact]
    public async Task Update_BackToTheLogger_RemovesTheSmtpSettings()
    {
        await Update(SmtpRequest());

        await Update(new EmailSettingsRequest("Logger", null));

        Assert.Equal("""{"Provider":"Logger"}""", Email().ToJsonString());
    }

    [Fact]
    public async Task Update_InvalidRequest_IsRejectedAndWritesNothing()
    {
        var before = File.ReadAllText(PathOf("email.json"));

        var result = await Update(new EmailSettingsRequest("Smtp", new SmtpSettingsRequest("", 587, true, null, "No", null, null, null)));

        Assert.IsType<BadRequest>(result);
        Assert.Equal(before, File.ReadAllText(PathOf("email.json")));
    }

    [Fact]
    public async Task Update_ReadOnlyConfiguration_IsForbidden()
    {
        var before = File.ReadAllText(PathOf("email.json"));

        var result = await Update(SmtpRequest(), readOnly: true);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<JsonHttpResult<ConfigurationForbiddenResponse>>(result).StatusCode);
        Assert.Equal(before, File.ReadAllText(PathOf("email.json")));
    }

    [Fact]
    public async Task GenericUpdate_RefusesTheEmailFile()
    {
        var request = new SettingsUpdateRequest(
            new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["Email:Provider"] = System.Text.Json.JsonDocument.Parse("\"Smtp\"").RootElement.Clone()
            });

        var result = await SettingsEndpoints.Update("email.json", request, Catalog, TestLocales.Embedded, Security(), _logger, CancellationToken.None);

        Assert.IsType<NotFound>(result);
        Assert.Equal("Logger", (string)Email()["Provider"]!);
    }
}
