using System.Runtime.CompilerServices;

namespace Behind.Tests.Helpers;

public static class TestcontainersEnvironment
{
    [ModuleInitializer]
    public static void Initialize()
    {
        var defaults = Resolve(
            Environment.GetEnvironmentVariable,
            File.Exists,
            File.Exists("/run/.containerenv") || File.Exists("/.dockerenv"));
        foreach (var (name, value) in defaults)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public static IReadOnlyDictionary<string, string> Resolve(
        Func<string, string?> environment,
        Func<string, bool> exists,
        bool inContainer)
    {
        if (!string.IsNullOrWhiteSpace(environment("DOCKER_HOST")))
        {
            return new Dictionary<string, string>();
        }

        var endpoint = environment("CONTAINER_HOST");
        if (string.IsNullOrWhiteSpace(endpoint))
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
                && string.IsNullOrWhiteSpace(environment("TESTCONTAINERS_HOST_OVERRIDE")))
            {
                values["TESTCONTAINERS_HOST_OVERRIDE"] = "host.containers.internal";
            }
        }

        return values;
    }
}
