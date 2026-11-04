using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Behind.Common.Configuration;

public sealed class ResilientJsonConfigurationSource : JsonConfigurationSource
{
    public Action<string, Exception>? OnReloadFailed { get; init; }

    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);

        return new ResilientJsonConfigurationProvider(this);
    }
}

public sealed class ResilientJsonConfigurationProvider(ResilientJsonConfigurationSource source)
    : JsonConfigurationProvider(source)
{
    private int _loaded;

    public override void Load(Stream stream)
    {
        try
        {
            base.Load(stream);
            Volatile.Write(ref _loaded, 1);
        }
        catch (Exception exception) when (Volatile.Read(ref _loaded) == 1)
        {
            source.OnReloadFailed?.Invoke(source.Path ?? string.Empty, exception);
        }
    }
}
