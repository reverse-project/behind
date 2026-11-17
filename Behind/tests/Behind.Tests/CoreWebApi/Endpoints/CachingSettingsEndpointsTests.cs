using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Settings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class CachingSettingsEndpointsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("behind-caching-settings-").FullName;
    private string FilePath => Path.Combine(_directory, "caching.json");
    private ConfigurationFileCatalog Catalog => ConfigurationFileCatalog.For(_directory);
    private static IOptions<SecurityConfiguration> Security(bool readOnly = false) =>
        Options.Create(new SecurityConfiguration(readOnly, None, None, None));

    public CachingSettingsEndpointsTests()
    {
        File.WriteAllText(FilePath, """
            {
              "Caching": { "Provider": "Memory", "Key": "Valkey" },
              "ConnectionStrings": { "Valkey": "valkey:6379,password=do-not-disclose" }
            }
            """);
    }

    public void Dispose() => Directory.Delete(_directory, true);

    private async Task<SettingsFileResponse> Listed()
    {
        var result = Assert.IsType<JsonHttpResult<SettingsResponse>>(await SettingsEndpoints.Get(
            Catalog, TestLocales.Embedded, Security(), CancellationToken.None));
        return Assert.Single(result.Value!.Files);
    }

    private Task<Microsoft.AspNetCore.Http.IResult> Save(string path, string value, bool readOnly = false) =>
        SettingsEndpoints.Update("caching.json",
            new SettingsUpdateRequest(new Dictionary<string, JsonElement>
            {
                [path] = JsonSerializer.SerializeToElement(value)
            }),
            Catalog, TestLocales.Embedded, Security(readOnly), NullLogger<SettingsEndpoints>.Instance,
            CancellationToken.None);

    [Fact]
    public async Task ListsCachingTabWithProviderSelectorKeyAndMaskedConnection()
    {
        var file = await Listed();
        Assert.Equal("caching.json", file.Name);
        Assert.Equal(new[] { "Memory", "RedisCompatible" }, file.Fields.Single(field =>
            field.Path == "Caching:Provider").Options!.Select(option => option.Value));
        Assert.Equal("Valkey", file.Fields.Single(field => field.Path == "Caching:Key").Value);
        Assert.True(file.Fields.Single(field => field.Path == "ConnectionStrings:Valkey").Sensitive);
        Assert.DoesNotContain("do-not-disclose", JsonSerializer.Serialize(file));
    }

    [Fact]
    public async Task SavesProviderAndKeyWithoutChangingStoredConnection()
    {
        Assert.IsType<NoContent>(await Save("Caching:Provider", "RedisCompatible"));
        Assert.IsType<NoContent>(await Save("Caching:Key", string.Empty));
        var json = JsonNode.Parse(File.ReadAllText(FilePath))!;
        Assert.Equal("RedisCompatible", json["Caching"]!["Provider"]!.GetValue<string>());
        Assert.Equal(string.Empty, json["Caching"]!["Key"]!.GetValue<string>());
        Assert.Equal("valkey:6379,password=do-not-disclose", json["ConnectionStrings"]!["Valkey"]!.GetValue<string>());
    }

    [Fact]
    public async Task MixedCasePathsAreListedOnceAndAcceptUpdates()
    {
        File.WriteAllText(FilePath, """{ "cAcHiNg": { "pRoViDeR": "Memory", "kEy": "Valkey" }, "cOnNeCtIoNsTrInGs": { "Valkey": "old" } }""");
        var file = await Listed();
        Assert.Single(file.Fields, field => string.Equals(field.Path, "Caching:Provider", StringComparison.OrdinalIgnoreCase));
        Assert.Single(file.Fields, field => string.Equals(field.Path, "Caching:Key", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(file.Fields.Single(field => field.Path == "cAcHiNg:pRoViDeR").Options);
        Assert.IsType<NoContent>(await Save("cAcHiNg:pRoViDeR", "RedisCompatible"));
        Assert.IsType<NoContent>(await Save("cAcHiNg:kEy", string.Empty));
        Assert.IsType<NoContent>(await Save("cOnNeCtIoNsTrInGs:Valkey", "replacement"));
        Assert.IsType<BadRequest>(await Save("cAcHiNg:pRoViDeR", "Unknown"));
        var root = JsonNode.Parse(File.ReadAllText(FilePath))!;
        Assert.Equal("RedisCompatible", root["cAcHiNg"]!["pRoViDeR"]!.GetValue<string>());
        Assert.Equal(string.Empty, root["cAcHiNg"]!["kEy"]!.GetValue<string>());
        Assert.Equal("replacement", root["cOnNeCtIoNsTrInGs"]!["Valkey"]!.GetValue<string>());
    }

    [Fact]
    public async Task RejectsInvalidProviderAndReadOnlyWritesWithoutChangingFile()
    {
        var before = File.ReadAllText(FilePath);
        Assert.IsType<BadRequest>(await Save("Caching:Provider", "Unknown"));
        Assert.IsType<BadRequest>(await Save("Caching:ConnectionString", "never-inline"));
        Assert.Equal(before, File.ReadAllText(FilePath));
        var forbidden = Assert.IsType<JsonHttpResult<ConfigurationForbiddenResponse>>(
            await Save("Caching:Provider", "RedisCompatible", true));
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Equal(before, File.ReadAllText(FilePath));
    }

    [Fact]
    public async Task ReplacesSecretAndDoesNotReturnIt()
    {
        Assert.IsType<NoContent>(await Save("ConnectionStrings:Valkey", "valkey:6379,password=replacement-secret"));
        Assert.DoesNotContain("replacement-secret", JsonSerializer.Serialize(await Listed()));
    }

    [Fact]
    public async Task MissingKeyIsEditableAndDefaultsToMemory()
    {
        File.WriteAllText(FilePath, """{ "Caching": {}, "ConnectionStrings": { "Valkey": "" } }""");
        var file = await Listed();
        Assert.Equal("Memory", file.Fields.Single(field => field.Path == "Caching:Provider").Value);
        Assert.Equal(string.Empty, file.Fields.Single(field => field.Path == "Caching:Key").Value);
        Assert.IsType<NoContent>(await Save("Caching:Key", "Valkey"));
    }
}
