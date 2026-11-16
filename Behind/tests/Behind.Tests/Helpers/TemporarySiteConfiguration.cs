using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Core.Tools;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.Helpers;

public sealed class TemporarySiteConfiguration : IDisposable
{
    private const string Password = "temporary-site-configuration";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public string Root { get; } = Directory.CreateTempSubdirectory("behind-site-configuration-").FullName;

    public string DataDirectory { get; }

    public string ConfigDirectory { get; }

    public TemporarySiteConfiguration(string site)
    {
        DataDirectory = Path.Combine(Root, "data", site);
        ConfigDirectory = Path.Combine(Root, "config", site);

        new StartupSetupRequest(
            DataDirectory,
            ConfigDirectory,
            StartupSetupRequest.DefaultIssuer,
            StartupSetupRequest.DefaultAudience,
            false,
            Password,
            false,
            false).Run(TextWriter.Null);
    }

    public string FilePath(string name) => Path.Combine(ConfigDirectory, name);

    public TemporarySiteConfiguration Write(string name, string json)
    {
        File.WriteAllText(FilePath(name), json);

        return this;
    }

    public TemporarySiteConfiguration Merge(string name, string json)
    {
        var target = File.Exists(FilePath(name))
            ? JsonNode.Parse(File.ReadAllText(FilePath(name)))!.AsObject()
            : new JsonObject();

        Merge(target, JsonNode.Parse(json)!.AsObject());

        return Write(name, target.ToJsonString(Indented));
    }

    public JsonElement ReadJson(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FilePath(name)));

        return document.RootElement.Clone();
    }

    public IConfigurationRoot Configuration(params string[] names) =>
        names
            .Aggregate(
                new ConfigurationBuilder().SetBasePath(ConfigDirectory),
                (builder, name) => (ConfigurationBuilder)builder.AddJsonFile(name, optional: false, reloadOnChange: false))
            .Build();

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source.ToList())
        {
            if (value is JsonObject child && target[key] is JsonObject existing)
            {
                Merge(existing, child);
            }
            else
            {
                target[key] = value?.DeepClone();
            }
        }
    }
}
