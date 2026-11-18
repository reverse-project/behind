using System.Runtime.CompilerServices;

namespace Behind.Tests.Helpers;

public static class TestcontainersEnvironment
{
    [ModuleInitializer]
    public static void Initialize()
    {
        var propertiesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".testcontainers.properties");
        var defaults = Resolve(
            Environment.GetEnvironmentVariable,
            File.Exists,
            File.Exists("/run/.containerenv") || File.Exists("/.dockerenv"),
            File.Exists(propertiesPath) ? File.ReadAllLines(propertiesPath) : []);
        foreach (var (name, value) in defaults)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public static IReadOnlyDictionary<string, string> Resolve(
        Func<string, string?> environment,
        Func<string, bool> exists,
        bool inContainer,
        IEnumerable<string>? propertiesLines = null)
    {
        var properties = (propertiesLines ?? [])
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith('#') && !line.StartsWith('!'))
            .Select(line => line.Split(['=', ':', ' '], 2, StringSplitOptions.RemoveEmptyEntries))
            .Where(property => property.Length == 2)
            .ToDictionary(property => property[0], property => property[1]);
        if (!string.IsNullOrWhiteSpace(environment("DOCKER_HOST"))
            || (properties.TryGetValue("docker.host", out var configuredHost)
                && !string.IsNullOrEmpty(configuredHost)
                && Uri.TryCreate(configuredHost, UriKind.RelativeOrAbsolute, out _)))
        {
            return new Dictionary<string, string>();
        }

        var endpoint = environment("CONTAINER_HOST");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            || endpointUri.Scheme is not ("unix" or "npipe" or "tcp" or "http" or "https"))
        {
            if (exists("/var/run/docker.sock"))
            {
                return new Dictionary<string, string>();
            }

            var runtime = environment("XDG_RUNTIME_DIR");
            string[] sockets =
            [
                "/run/podman/podman.sock",
                ..string.IsNullOrWhiteSpace(runtime) ? Array.Empty<string>() : [Path.Combine(runtime, "podman", "podman.sock")]
            ];
            var socket = sockets.FirstOrDefault(exists);
            if (socket is null)
            {
                return new Dictionary<string, string>();
            }

            endpoint = $"unix://{socket}";
        }

        var values = new Dictionary<string, string> { ["DOCKER_HOST"] = endpoint };
        if (endpoint.Contains("/podman/", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(environment("TESTCONTAINERS_RYUK_DISABLED")))
            {
                values["TESTCONTAINERS_RYUK_DISABLED"] = "true";
            }

            if (inContainer && endpoint == "unix:///run/podman/podman.sock"
                && string.IsNullOrWhiteSpace(environment("TESTCONTAINERS_HOST_OVERRIDE"))
                && string.IsNullOrEmpty(properties.GetValueOrDefault("host.override")))
            {
                values["TESTCONTAINERS_HOST_OVERRIDE"] = "host.containers.internal";
            }
        }

        return values;
    }
}
