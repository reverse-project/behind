using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common.Configuration;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Behind.Core.Common.Configuration;
using Behind.Core.Tools;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Settings;
using Behind.Forum.Common;
using Behind.MarkdownViewer.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Behind.Tests.Common;

public sealed class ConfigurationSchemaTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("behind-schema-").FullName;

    public void Dispose() => Directory.Delete(_directory, true);

    [Theory]
    [InlineData("AuditSearchMaxEntries")]
    [InlineData("AuditSearchMaxCandidates")]
    public void AuditSearchBudgets_HaveNumericSchemaAndPreserveDefaultsWhenOmitted(string property)
    {
        Assert.Equal("configuration-1.6.0", ConfigurationSchema.Name);
        Assert.True(ConfigurationSchema.IsValid(JsonNode.Parse("""{"Backend":{}}""")));
        var root = new JsonObject { ["Backend"] = new JsonObject { [property] = 10000 } };
        Assert.True(ConfigurationSchema.IsValid(root));
        root["Backend"]![property] = 1.5;
        Assert.False(ConfigurationSchema.IsValid(root));
        root["Backend"]![property] = (long)int.MaxValue + 1;
        Assert.False(ConfigurationSchema.IsValid(root));
        root["Backend"]![property] = "10000";
        Assert.False(ConfigurationSchema.IsValid(root));
        Assert.Equal(10000, new BackendConfiguration().AuditSearchMaxEntries);
        Assert.Equal(10000, new BackendConfiguration().AuditSearchMaxCandidates);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(10000, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void SignInNotificationTimeoutRequiresAPositiveInteger(int milliseconds, bool expected)
    {
        var root = new JsonObject
        {
            ["Security"] = new JsonObject { ["SignInNotificationTimeoutMilliseconds"] = milliseconds }
        };

        Assert.Equal(expected, ConfigurationSchema.IsValid(root));
        Assert.Equal(SecurityConfiguration.DefaultSignInNotificationTimeoutMilliseconds,
            new SecurityConfiguration().SignInNotificationTimeoutMilliseconds);
    }

    [Fact]
    public void AllConfigurationSectionRecords_HaveDefinitionsForEveryWritableProperty()
    {
        var definitions = ConfigurationSchema.Describe()["$defs"]!.AsObject();
        var types = new[]
            {
                typeof(CachingConfiguration).Assembly, typeof(DatabaseConfiguration).Assembly,
                typeof(ForumSettings).Assembly, typeof(MarkdownViewerConfiguration).Assembly
            }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && type.GetField("SectionName",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly) is not null)
            .Concat(
            [
                typeof(FrontendOptions), typeof(Behind.Core.Common.Configuration.Site), typeof(JwtSettings), typeof(MagicLinkSettings),
                typeof(DataProtectionSettings), typeof(ElevationSettings), typeof(OidcProviderConfiguration),
                typeof(SmtpEmailConfiguration), typeof(LoggerEmailConfiguration),
                typeof(Ssl), typeof(BasicAuthentication), typeof(NoAuthentication),
                typeof(OpenTelemetryConfiguration.OtlpExporterConfiguration),
                typeof(OpenTelemetryConfiguration.LoggingConfiguration)
            ]);

        foreach (var type in types)
        {
            var schema = Assert.IsType<JsonObject>(definitions[type.Name]);
            var properties = PropertyNames(schema, definitions).ToHashSet(StringComparer.Ordinal);
            foreach (var property in type.GetProperties().Where(property => property.SetMethod is not null))
            {
                Assert.True(properties.Contains(property.Name), $"{type.Name}.{property.Name} has no schema.");
            }
        }
    }

    private static IEnumerable<string> PropertyNames(JsonObject schema, JsonObject definitions)
    {
        if (schema["$ref"] is JsonValue reference)
        {
            return PropertyNames(definitions[reference.GetValue<string>().Split('/')[^1]]!.AsObject(), definitions);
        }

        return (schema["properties"] as JsonObject ?? []).Select(property => property.Key)
            .Concat((schema["allOf"] as JsonArray ?? []).OfType<JsonObject>()
                .SelectMany(parent => PropertyNames(parent, definitions)));
    }

    [Fact]
    public void GeneratedConfigurationFiles_AllPassTheirSchema()
    {
        new StartupSetupRequest(Path.Combine(_directory, "data"), _directory, "issuer", "audience",
            false, "password", false, false).Run(TextWriter.Null);

        foreach (var path in Directory.GetFiles(_directory, "*.json"))
        {
            Assert.True(ConfigurationSchema.IsValid(JsonNode.Parse(File.ReadAllText(path))),
                $"{Path.GetFileName(path)} does not match the configuration schema.");
        }
    }

    [Theory]
    [InlineData("backend.json", """{"Backend":{"Accounts":{"Password":true}}}""", "Backend:Accounts:Password", "false", true)]
    [InlineData("backend.json", """{"Backend":{"Accounts":{"Password":true}}}""", "Backend:Accounts:Password", "\"false\"", false)]
    [InlineData("backend.json", """{"Backend":{"Scheduler":{"TickSeconds":60}}}""", "Backend:Scheduler:TickSeconds", "-1", true)]
    [InlineData("backend.json", """{"Backend":{"Scheduler":{"TickSeconds":60}}}""", "Backend:Scheduler:TickSeconds", "1.5", true)]
    [InlineData("backend.json", """{"Backend":{"Scheduler":{"TickSeconds":60}}}""", "Backend:Scheduler:TickSeconds", "\"60\"", false)]
    [InlineData("caching.json", """{"Caching":{"Provider":"Memory"}}""", "Caching:Provider", "\"RedisCompatible\"", true)]
    [InlineData("caching.json", """{"Caching":{"Provider":"Memory"}}""", "Caching:Provider", "\"rediscompatible\"", true)]
    [InlineData("caching.json", """{"Caching":{"Provider":"Memory"}}""", "Caching:Provider", "\"Other\"", false)]
    [InlineData("caching.json", """{"Caching":{"Key":"Valkey"}}""", "Caching:Key", "\"\"", true)]
    [InlineData("database.json", """{"Database":{"Provider":"Sqlite"}}""", "Database:Provider", "\"Npgsql\"", true)]
    public void Schema_MatchesExistingGenericSettingsAcceptance(
        string file, string stored, string path, string incoming, bool expected)
    {
        var root = JsonNode.Parse(stored)!.AsObject();
        var value = JsonSerializer.Deserialize<JsonElement>(incoming);
        var planned = root.PlanChanges(file, new Dictionary<string, JsonElement> { [path] = value });
        Assert.Equal(expected, planned.AsEnumerable().Any());
        var segments = path.Split(':');
        var parent = segments[..^1].Aggregate((JsonNode)root, (node, part) => node[part]!);
        parent[segments[^1]] = JsonNode.Parse(incoming);
        Assert.Equal(expected, ConfigurationSchema.IsValid(root));
    }

    [Fact]
    public async Task InvalidCandidate_IsRejectedBeforeWritingAnyBytes()
    {
        var path = Path.Combine(_directory, "backend.json");
        const string original = """{ "Backend": { "Accounts": { "Password": true } } }""";
        await File.WriteAllTextAsync(path, original);

        await Assert.ThrowsAsync<ConfigurationSchemaException>(() => new FileInfo(path).SetJsonNodesAsync(
            new Dictionary<string, JsonNode?> { ["Backend:Accounts:Password"] = JsonValue.Create("not a boolean") }));

        Assert.Equal(original, await File.ReadAllTextAsync(path));
        Assert.Single(Directory.GetFiles(_directory));
        await new FileInfo(path).SetJsonNodesAsync(
            new Dictionary<string, JsonNode?> { ["Backend:Accounts:Password"] = JsonValue.Create(false) });
        Assert.False(JsonNode.Parse(await File.ReadAllTextAsync(path))!["Backend"]!["Accounts"]!["Password"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("rediscompatible")]
    [InlineData("MEMORY")]
    public async Task ExistingCachingProviderCasing_DoesNotPreventUnrelatedChanges(string provider)
    {
        var path = Path.Combine(_directory, "caching.json");
        await File.WriteAllTextAsync(path, $$$"""{"Caching":{"Provider":"{{{provider}}}"},"ConnectionStrings":{"Cache":"old"}}""");

        await new FileInfo(path).SetJsonValuesAsync(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Cache"] = "new"
        });

        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        Assert.Equal(provider, root["Caching"]!["Provider"]!.GetValue<string>());
        Assert.Equal("new", root["ConnectionStrings"]!["Cache"]!.GetValue<string>());
    }

    [Fact]
    public async Task Settings_ReturnsBadRequestAndPreservesFile_WhenCandidateFailsSchema()
    {
        var path = Path.Combine(_directory, "backend.json");
        const string original = """{"Backend":{"Accounts":{"Password":"legacy-invalid"}}}""";
        await File.WriteAllTextAsync(path, original);
        var result = await SettingsEndpoints.Update("backend.json",
            new SettingsUpdateRequest(new Dictionary<string, JsonElement>
            {
                ["Backend:Accounts:Password"] = JsonSerializer.SerializeToElement("still-invalid")
            }), ConfigurationFileCatalog.For(_directory), TestLocales.Embedded,
            Options.Create(new SecurityConfiguration()), NullLogger<SettingsEndpoints>.Instance, CancellationToken.None);

        Assert.IsType<BadRequest>(result);
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("""{"Caching":null}""")]
    [InlineData("""{"Caching":{},"ConnectionStrings":{"Valkey":null}}""")]
    [InlineData("""{"Database":{"Provider":null,"Key":null},"Security":{"MagicLink":{"RequestLimit":null,"RedeemLimit":null}}}""")]
    [InlineData("""{"Backend":{"Scheduler":{"RunTimeoutMinutes":0,"TickSeconds":-1}},"Custom":{"Keep":42}}""")]
    [InlineData("""{"Email":{"Provider":"Smtp","Templates":{"Welcome":"<b>Hello</b>"}}}""")]
    [InlineData("""{"Security":{"MagicLink":{"ActualSecret":"uuid://019a8b31-c000-7000-8000-000000000001"}}}""")]
    public void Schema_PreservesDefaultsFallbacksCustomSectionsAndConverterShapes(string json)
    {
        Assert.True(ConfigurationSchema.IsValid(JsonNode.Parse(json)));
    }

    [Theory]
    [InlineData("""{"security":{"accounts":{"subaccounts":false,"linkunlink":true}}}""", true)]
    [InlineData("""{"Security":{"Accounts":{"Subaccounts":"false"}}}""", false)]
    [InlineData("""{"Security":{"Accounts":{"LinkUnlink":1}}}""", false)]
    [InlineData("""{"backend":{"accounts":{"password":false}}}""", true)]
    [InlineData("""{"backend":{"accounts":{"password":"false"}}}""", false)]
    [InlineData("""{"SECURITY":{"Oidc":{"Providers":[{"RoleMappings":{"staff":["Staff"],"Staff":["Admin"]}}]}}}""", true)]
    [InlineData("""{"opentelemetry":{"logging":{"isenabled":"false"}}}""", false)]
    [InlineData("""{"security":{"accounts":{"switchpinmaxlength":1}}}""", true)]
    [InlineData("""{"Security":{"Accounts":{"SwitchPinMaxLength":128}}}""", true)]
    [InlineData("""{"Security":{"Accounts":{"SwitchPinMaxLength":129}}}""", true)]
    [InlineData("""{"Security":{"Accounts":{"SwitchPinMaxLength":2147483648}}}""", false)]
    [InlineData("""{"Backend":{"Accounts":{"SubaccountSwitch":false}}}""", true)]
    [InlineData("""{"Backend":{"Accounts":{"SubaccountSwitch":"false"}}}""", false)]
    [InlineData("""{"Security":{"Accounts":{"SwitchPinMaxLength":0}}}""", false)]
    [InlineData("""{"Security":{"Accounts":{"SwitchPinMaxLength":"12"}}}""", false)]
    [InlineData("""{"Security":{"Accounts":{"SwitchPinMaxLength":1.5}}}""", false)]
    public void Schema_MatchesCaseInsensitiveConfigurationNames(string json, bool valid)
    {
        Assert.Equal(valid, ConfigurationSchema.IsValid(JsonNode.Parse(json)));
    }

    [Theory]
    [InlineData("""{"Caching":null}""", true)]
    [InlineData("""{"Caching":{"Provider":"Memory","Key":null},"ConnectionStrings":{"Valkey":null}}""", true)]
    [InlineData("""{"Caching":{"Provider":"Other"}}""", false)]
    public void CorrectedCachingSchema_MatchesRuntimeFallbacks(string json, bool valid)
    {
        var node = JsonNode.Parse(json);
        Assert.Equal(valid, ConfigurationSchema.IsValid(node));
        Assert.Equal(valid, JsonSchemas.IsValid("caching-configuration-1.1.0", node));
    }

    [Theory]
    [InlineData("4000000.0", 4000000L)]
    [InlineData("4e6", 4000000L)]
    [InlineData("1.000e3", 1000L)]
    [InlineData("0.0", 0L)]
    [InlineData("-1.0", -1L)]
    [InlineData("9223372036854775807.0", long.MaxValue)]
    [InlineData("-9223372036854775808.0", long.MinValue)]
    public async Task GalleryPixelBudget_NormalizesIntegralNumbersBeforeWriting(string incoming, long expected)
    {
        var path = Path.Combine(_directory, "frontend.json");
        await File.WriteAllTextAsync(path, """{"frontend":{"galleries":{"maxdecodedpixels":4000000}},"Custom":{"keep":1.5}}""");
        await new FileInfo(path).SetJsonNodesAsync(new Dictionary<string, JsonNode?>
        {
            ["frontend:galleries:maxdecodedpixels"] = JsonNode.Parse(incoming)
        });

        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        Assert.Equal(expected, root["frontend"]!["galleries"]!["maxdecodedpixels"]!.GetValue<long>());
        Assert.Equal(1.5, root["Custom"]!["keep"]!.GetValue<double>());
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(path).Build().GetSection("Frontend:Galleries").Get<GalleriesConfiguration>()!;
        Assert.Equal(expected, configuration.MaxDecodedPixels);
        Assert.Equal(expected > 0 ? expected : ImageThumbnailExtensions.MaxPixels, configuration.DecodePixelLimit);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("4000000.0000000000000000000001")]
    [InlineData("1e-100")]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    [InlineData("1e1000")]
    [InlineData("\"4000000\"")]
    public async Task GalleryPixelBudget_RejectsInvalidNumbersWithoutChangingFile(string incoming)
    {
        var path = Path.Combine(_directory, "frontend.json");
        const string original = """{"Frontend":{"Galleries":{"MaxDecodedPixels":4000000}}}""";
        await File.WriteAllTextAsync(path, original);
        await Assert.ThrowsAsync<ConfigurationSchemaException>(() => new FileInfo(path).SetJsonNodesAsync(
            new Dictionary<string, JsonNode?> { ["Frontend:Galleries:MaxDecodedPixels"] = JsonNode.Parse(incoming) }));
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("4000000.0", true)]
    [InlineData("1.5", false)]
    [InlineData("9223372036854775808", false)]
    public async Task Settings_GalleryPixelBudgetValidatesAndNormalizesBeforeSaving(string incoming, bool accepted)
    {
        var path = Path.Combine(_directory, "frontend.json");
        const string original = """{"Frontend":{"Galleries":{"MaxDecodedPixels":4000000}}}""";
        await File.WriteAllTextAsync(path, original);
        var result = await SettingsEndpoints.Update("frontend.json",
            new SettingsUpdateRequest(new Dictionary<string, JsonElement>
            {
                ["Frontend:Galleries:MaxDecodedPixels"] = JsonSerializer.Deserialize<JsonElement>(incoming)
            }), ConfigurationFileCatalog.For(_directory), TestLocales.Embedded,
            Options.Create(new SecurityConfiguration()), NullLogger<SettingsEndpoints>.Instance, CancellationToken.None);

        if (accepted)
        {
            Assert.IsType<NoContent>(result);
            var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            Assert.Equal(4000000L, root["Frontend"]!["Galleries"]!["MaxDecodedPixels"]!.GetValue<long>());
        }
        else
        {
            Assert.IsType<BadRequest>(result);
            Assert.Equal(original, await File.ReadAllTextAsync(path));
        }
    }

    [Fact]
    public void GalleryPixelBudget_OmissionPreservesDefaultAndEarlierSchemaRemainsUnchanged()
    {
        Assert.True(ConfigurationSchema.IsValid(JsonNode.Parse("""{"Frontend":{"Galleries":{}}}""")));
        Assert.True(JsonSchemas.IsValid("configuration-1.0.1",
            JsonNode.Parse("""{"Frontend":{"Galleries":{"MaxDecodedPixels":1.5}}}""")));
        Assert.False(JsonSchemas.IsValid(ConfigurationSchema.Name,
            JsonNode.Parse("""{"Frontend":{"Galleries":{"MaxDecodedPixels":1.5}}}""")));
        Assert.Equal(ImageThumbnailExtensions.MaxPixels, new GalleriesConfiguration().DecodePixelLimit);
    }
}
