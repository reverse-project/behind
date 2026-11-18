namespace Behind.Tests.Common;

public sealed class TestcontainersEnvironmentTests
{
    [Fact]
    public void ExplicitDockerEndpoint_RemainsAuthoritative()
    {
        var values = Resolve(new Dictionary<string, string>
        {
            ["DOCKER_HOST"] = "tcp://docker.example:2376",
            ["CONTAINER_HOST"] = "unix:///run/podman/podman.sock"
        }, ["/run/podman/podman.sock"], true);

        Assert.Empty(values);
    }

    [Fact]
    public void PodmanContainerHost_ConfiguresAllTestRunnersBeforeContainerCreation()
    {
        var values = Resolve(new Dictionary<string, string>
        {
            ["CONTAINER_HOST"] = "unix:///run/podman/podman.sock"
        }, [], true);

        Assert.Equal("unix:///run/podman/podman.sock", values["DOCKER_HOST"]);
        Assert.Equal("true", values["TESTCONTAINERS_RYUK_DISABLED"]);
        Assert.Equal("host.containers.internal", values["TESTCONTAINERS_HOST_OVERRIDE"]);
    }

    [Fact]
    public void MountedPodmanSocket_IsDiscoveredWithoutEnvironmentVariables()
    {
        var values = Resolve([], ["/run/podman/podman.sock"], true);

        Assert.Equal("unix:///run/podman/podman.sock", values["DOCKER_HOST"]);
        Assert.Equal("host.containers.internal", values["TESTCONTAINERS_HOST_OVERRIDE"]);
    }

    [Fact]
    public void HostRootlessSocket_UsesRuntimeDirectoryWithoutContainerHostOverride()
    {
        var values = Resolve(new Dictionary<string, string> { ["XDG_RUNTIME_DIR"] = "/run/user/1000" },
            ["/run/user/1000/podman/podman.sock"], false);

        Assert.Equal("unix:///run/user/1000/podman/podman.sock", values["DOCKER_HOST"]);
        Assert.Equal("true", values["TESTCONTAINERS_RYUK_DISABLED"]);
        Assert.False(values.ContainsKey("TESTCONTAINERS_HOST_OVERRIDE"));
    }

    [Fact]
    public void ExplicitHostAndRyukOverrides_ArePreserved()
    {
        var values = Resolve(new Dictionary<string, string>
        {
            ["CONTAINER_HOST"] = "unix:///run/podman/podman.sock",
            ["TESTCONTAINERS_HOST_OVERRIDE"] = "reachable.example",
            ["TESTCONTAINERS_RYUK_DISABLED"] = "false"
        }, [], true);

        Assert.Single(values);
        Assert.Equal("unix:///run/podman/podman.sock", values["DOCKER_HOST"]);
    }

    [Fact]
    public void DockerSocket_KeepsStandardDiscoveryAndResourceReaper()
    {
        Assert.Empty(Resolve([], ["/var/run/docker.sock", "/run/podman/podman.sock"], true));
    }

    [Fact]
    public void MissingEngine_DoesNotInventAnEndpointOrSkipIntegrationTests()
    {
        Assert.Empty(Resolve([], [], false));
    }

    [Fact]
    public void RemoteContainerHost_IsPassedThroughWithoutDisablingDockerRyuk()
    {
        var values = Resolve(new Dictionary<string, string> { ["CONTAINER_HOST"] = "tcp://engine.example:2376" }, [], true);

        Assert.Single(values);
        Assert.Equal("tcp://engine.example:2376", values["DOCKER_HOST"]);
    }

    [Theory]
    [InlineData("docker.host=tcp://engine.example:2376")]
    [InlineData("docker.host:tcp://engine.example:2376")]
    [InlineData("  docker.host = tcp://engine.example:2376  ")]
    public void PropertiesDockerEndpoint_RemainsAuthoritative(string property)
    {
        Assert.Empty(Resolve(new Dictionary<string, string>
        {
            ["CONTAINER_HOST"] = "unix:///run/podman/podman.sock"
        }, ["/run/podman/podman.sock"], true, [property]));
    }

    [Fact]
    public void PropertiesHostOverride_IsPreserved()
    {
        var values = Resolve([], ["/run/podman/podman.sock"], true,
            ["# host.override=ignored.example", "host.override=reachable.example"]);

        Assert.Equal("unix:///run/podman/podman.sock", values["DOCKER_HOST"]);
        Assert.False(values.ContainsKey("TESTCONTAINERS_HOST_OVERRIDE"));
    }

    [Theory]
    [InlineData("unix:///forwarded/podman.sock")]
    [InlineData("npipe://./pipe/docker_engine")]
    [InlineData("tcp://engine.example:2376")]
    [InlineData("http://engine.example:2375")]
    [InlineData("https://engine.example:2376")]
    public void SupportedContainerHost_IsForwarded(string endpoint)
    {
        var values = Resolve(new Dictionary<string, string> { ["CONTAINER_HOST"] = endpoint }, [], false);

        Assert.Equal(endpoint, values["DOCKER_HOST"]);
    }

    [Fact]
    public void SshContainerHost_WithoutSocketKeepsStandardDiscovery()
    {
        Assert.Empty(Resolve(new Dictionary<string, string>
        {
            ["CONTAINER_HOST"] = "ssh://user@engine.example/run/podman/podman.sock"
        }, [], true));
    }

    [Fact]
    public void SshContainerHost_UsesAvailableForwardedSocket()
    {
        var values = Resolve(new Dictionary<string, string>
        {
            ["CONTAINER_HOST"] = "ssh://user@engine.example/run/podman/podman.sock"
        }, ["/run/podman/podman.sock"], true);

        Assert.Equal("unix:///run/podman/podman.sock", values["DOCKER_HOST"]);
    }

    private static IReadOnlyDictionary<string, string> Resolve(
        Dictionary<string, string> environment, string[] sockets, bool inContainer, string[]? properties = null) =>
        TestcontainersEnvironment.Resolve(
            name => environment.GetValueOrDefault(name),
            path => sockets.Contains(path, StringComparer.Ordinal),
            inContainer,
            properties);
}
