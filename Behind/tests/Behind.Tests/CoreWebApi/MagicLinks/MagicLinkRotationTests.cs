using System.Text.Json.Nodes;
using Behind.Common.Configuration;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.MagicLinks;
using Behind.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.MagicLinks;

public sealed class MagicLinkRotationTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(10);
    private static readonly TimeSpan Overlap = TimeSpan.FromDays(2);

    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, true);

    private static MagicLinkSettings SettingsCreatedAt(DateTimeOffset createdAt, Option<Uuid> old = default) =>
        new(Uuid.CreateVersion7(createdAt), old, Some(Interval), Some(Overlap));

    private static MagicLinkSecretRotator RotatorAt(
        DateTimeOffset now,
        IMagicLinkSettingsStore store,
        IMagicLinkSettingsPersister persister) =>
        new(store, persister, new FixedTimeProvider(now), Substitute.For<ILogger<MagicLinkSecretRotator>>());

    private static MagicLinkSettings CurrentOf(IMagicLinkSettingsStore store) =>
        store.Current.Match(() => throw new InvalidOperationException(), settings => settings);

    private async Task<JsonObject> MagicLinkSectionOf(string path) =>
        JsonNode.Parse(await File.ReadAllTextAsync(path))!["Security"]!["MagicLink"]!.AsObject();

    [Fact]
    public void Store_ReplaceMakesNewSettingsCurrent()
    {
        var initial = SettingsCreatedAt(Start);
        var store = new MagicLinkSettingsStore(Some(initial));
        var replacement = initial.Rotate(Start + Interval);

        store.Replace(replacement);

        Assert.Equal(Some(replacement), store.Current);
    }

    [Fact]
    public void Store_WithoutSettings_IsNone() =>
        Assert.Equal(None, new MagicLinkSettingsStore(None).Current);

    [Fact]
    public async Task Rotator_NotDue_DoesNothing()
    {
        var settings = SettingsCreatedAt(Start);
        var store = new MagicLinkSettingsStore(Some(settings));
        var persister = Substitute.For<IMagicLinkSettingsPersister>();

        var rotated = await RotatorAt(Start + Interval - TimeSpan.FromMinutes(1), store, persister)
            .RotateIfNeededAsync();

        Assert.False(rotated);
        Assert.Equal(Some(settings), store.Current);
        await persister.DidNotReceiveWithAnyArgs().PersistAsync(default!, default);
    }

    [Fact]
    public async Task Rotator_Due_RotatesKeepsOldAndPersists()
    {
        var settings = SettingsCreatedAt(Start);
        var store = new MagicLinkSettingsStore(Some(settings));
        var persister = Substitute.For<IMagicLinkSettingsPersister>();
        var now = Start + Interval;

        var rotated = await RotatorAt(now, store, persister).RotateIfNeededAsync();

        var current = CurrentOf(store);
        Assert.True(rotated);
        Assert.Equal(Some(settings.ActualSecret), current.OldSecret);
        Assert.Equal(Some(now), current.ActualSecret.CreatedAt);
        Assert.Equal([current.ActualSecret, settings.ActualSecret], current.ValidSecrets(now));
        await persister.Received(1).PersistAsync(current, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rotator_AfterOverlap_DropsOldSecret()
    {
        var store = new MagicLinkSettingsStore(Some(SettingsCreatedAt(Start)));
        var persister = Substitute.For<IMagicLinkSettingsPersister>();
        var rotationTime = Start + Interval;

        await RotatorAt(rotationTime, store, persister).RotateIfNeededAsync();
        var dropped = await RotatorAt(rotationTime + Overlap, store, persister).RotateIfNeededAsync();

        Assert.True(dropped);
        Assert.Equal(None, CurrentOf(store).OldSecret);
        await persister.Received(2).PersistAsync(Arg.Any<MagicLinkSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rotator_WithoutSettings_DoesNothing()
    {
        var persister = Substitute.For<IMagicLinkSettingsPersister>();

        var rotated = await RotatorAt(Start, new MagicLinkSettingsStore(None), persister).RotateIfNeededAsync();

        Assert.False(rotated);
        await persister.DidNotReceiveWithAnyArgs().PersistAsync(default!, default);
    }

    [Fact]
    public async Task Rotator_RotateNow_RotatesBeforeTheIntervalAndPersists()
    {
        var settings = SettingsCreatedAt(Start);
        var store = new MagicLinkSettingsStore(Some(settings));
        var persister = Substitute.For<IMagicLinkSettingsPersister>();
        var now = Start + TimeSpan.FromDays(1);

        var rotated = await RotatorAt(now, store, persister).RotateNowAsync();

        var current = CurrentOf(store);
        Assert.True(rotated);
        Assert.Equal(Some(settings.ActualSecret), current.OldSecret);
        Assert.Equal(Some(now), current.ActualSecret.CreatedAt);
        await persister.Received(1).PersistAsync(current, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rotator_RotateNow_WithoutSettings_DoesNothing()
    {
        var persister = Substitute.For<IMagicLinkSettingsPersister>();

        var rotated = await RotatorAt(Start, new MagicLinkSettingsStore(None), persister).RotateNowAsync();

        Assert.False(rotated);
        await persister.DidNotReceiveWithAnyArgs().PersistAsync(default!, default);
    }

    [Fact]
    public async Task Rotator_RotateNow_PersistFailure_KeepsTheCurrentSecrets()
    {
        var settings = SettingsCreatedAt(Start);
        var store = new MagicLinkSettingsStore(Some(settings));
        var persister = Substitute.For<IMagicLinkSettingsPersister>();
        persister.PersistAsync(Arg.Any<MagicLinkSettings>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("disk")));

        await Assert.ThrowsAsync<IOException>(() => RotatorAt(Start, store, persister).RotateNowAsync());

        Assert.Equal(Some(settings), store.Current);
    }

    [Fact]
    public async Task Rotator_PersistFailure_KeepsRotationInMemoryAndLogs()
    {
        var settings = SettingsCreatedAt(Start);
        var store = new MagicLinkSettingsStore(Some(settings));
        var persister = Substitute.For<IMagicLinkSettingsPersister>();
        persister.PersistAsync(Arg.Any<MagicLinkSettings>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("disk")));
        var logger = Substitute.For<ILogger<MagicLinkSecretRotator>>();
        var rotator = new MagicLinkSecretRotator(store, persister, new FixedTimeProvider(Start + Interval), logger);

        var rotated = await rotator.RotateIfNeededAsync();

        Assert.True(rotated);
        Assert.NotEqual(Some(settings), store.Current);
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<IOException>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Persister_WritesBothSecretsAndKeepsOtherSettings()
    {
        var path = Path.Combine(_directory, "security.json");
        await File.WriteAllTextAsync(path, """{ "Security": { "MagicLink": { "RotationInterval": "10.00:00:00" } } }""");
        var settings = SettingsCreatedAt(Start).Rotate(Start + Interval);

        await new JsonFileMagicLinkSettingsPersister(new FileInfo(path)).PersistAsync(settings);

        var section = await MagicLinkSectionOf(path);
        Assert.Equal(settings.ActualSecret.ToConfigurationValue(), (string?)section["ActualSecret"]);
        Assert.Equal(settings.OldSecret.Match(() => "", s => s.ToConfigurationValue()), (string?)section["OldSecret"]);
        Assert.Equal("10.00:00:00", (string?)section["RotationInterval"]);
    }

    [Fact]
    public async Task Persister_WithoutOldSecret_RemovesOldSecretEntry()
    {
        var path = Path.Combine(_directory, "security.json");
        await File.WriteAllTextAsync(path, """{ "Security": { "MagicLink": { "OldSecret": "uuid://x" } } }""");

        await new JsonFileMagicLinkSettingsPersister(new FileInfo(path)).PersistAsync(SettingsCreatedAt(Start));

        Assert.False((await MagicLinkSectionOf(path)).ContainsKey("OldSecret"));
    }

    [Fact]
    public async Task NoOpPersister_Completes() =>
        await new NoOpMagicLinkSettingsPersister().PersistAsync(SettingsCreatedAt(Start));

    [Fact]
    public async Task Service_RotatesOnStartup()
    {
        var store = new MagicLinkSettingsStore(Some(SettingsCreatedAt(Start)));
        var rotator = RotatorAt(Start + Interval, store, Substitute.For<IMagicLinkSettingsPersister>());
        using var service = new MagicLinkSecretRotationService(rotator, TimeProvider.System);

        await service.StartAsync(CancellationToken.None);
        await Wait.UntilAsync(() => CurrentOf(store).ActualSecret.CreatedAt, created => created == Some(Start + Interval));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(Some(Start + Interval), CurrentOf(store).ActualSecret.CreatedAt);
    }

    private ServiceProvider ProviderWith(SecurityConfiguration security, string fileName = "missing.json")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var option = new ReloadableOption<SecurityConfiguration>(new ConfigurationBuilder().Build(), _ => security);
        services.AddSingleton<IOptions<SecurityConfiguration>>(option);
        services.AddSingleton<IOptionsMonitor<SecurityConfiguration>>(option);
        services.AddMagicLink(new FileInfo(Path.Combine(_directory, fileName)));

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddMagicLink_RegistersRotationInfrastructure()
    {
        var services = new ServiceCollection();

        services.AddMagicLink(new FileInfo(Path.Combine(_directory, "missing.json")));

        Assert.Contains(services, d => d.ServiceType == typeof(IMagicLinkSettingsStore));
        Assert.Contains(services, d => d.ServiceType == typeof(MagicLinkSecretRotator));
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService)
                                       && d.ImplementationType == typeof(MagicLinkSecretRotationService));
    }

    [Fact]
    public void AddMagicLink_StoreStartsWithTheConfiguredSettings()
    {
        var settings = SettingsCreatedAt(Start);
        using var provider = ProviderWith(new SecurityConfiguration(false, None, None, Some(settings)));

        Assert.Equal(Some(settings), provider.GetRequiredService<IMagicLinkSettingsStore>().Current);
    }

    [Fact]
    public async Task AddMagicLink_ExistingWritableFile_PersistsToFile()
    {
        var path = Path.Combine(_directory, "security.json");
        await File.WriteAllTextAsync(path, "{}");
        var settings = SettingsCreatedAt(Start);
        using var provider = ProviderWith(new SecurityConfiguration(false, None, None, Some(settings)), "security.json");

        await provider.GetRequiredService<IMagicLinkSettingsPersister>().PersistAsync(settings);

        Assert.Equal(settings.ActualSecret.ToConfigurationValue(), (await MagicLinkSectionOf(path))["ActualSecret"]!.GetValue<string>());
    }

    [Fact]
    public async Task AddMagicLink_MissingFile_DoesNotPersist()
    {
        var settings = SettingsCreatedAt(Start);
        using var provider = ProviderWith(new SecurityConfiguration(false, None, None, Some(settings)));

        await provider.GetRequiredService<IMagicLinkSettingsPersister>().PersistAsync(settings);

        Assert.False(File.Exists(Path.Combine(_directory, "missing.json")));
    }

    [Fact]
    public async Task AddMagicLink_ReadOnlyConfiguration_DoesNotPersist()
    {
        var path = Path.Combine(_directory, "security.json");
        await File.WriteAllTextAsync(path, "{}");
        var settings = SettingsCreatedAt(Start);
        using var provider = ProviderWith(new SecurityConfiguration(true, None, None, Some(settings)), "security.json");

        await provider.GetRequiredService<IMagicLinkSettingsPersister>().PersistAsync(settings);

        Assert.Equal("{}", await File.ReadAllTextAsync(path));
    }
}
