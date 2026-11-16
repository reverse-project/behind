using Microsoft.Extensions.Configuration;

namespace Behind.Tests.Helpers;

public sealed class ReloadableMemoryConfiguration : IConfigurationRoot
{
    private readonly ReloadableMemoryProvider _provider = new();
    private readonly IConfigurationRoot _root;

    public ReloadableMemoryConfiguration(IReadOnlyDictionary<string, string?>? initial = null)
    {
        initial?.ToList().ForEach(pair => _provider.Set(pair.Key, pair.Value));
        _root = new ConfigurationRoot([_provider]);
    }

    public string? this[string key]
    {
        get => _root[key];
        set => _root[key] = value;
    }

    public IEnumerable<IConfigurationProvider> Providers => _root.Providers;

    public void Update(string key, string? value) => _provider.Update(key, value);

    public void Reload() => _root.Reload();

    public IEnumerable<IConfigurationSection> GetChildren() => _root.GetChildren();

    public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => _root.GetReloadToken();

    public IConfigurationSection GetSection(string key) => _root.GetSection(key);

    private sealed class ReloadableMemoryProvider : ConfigurationProvider
    {
        public void Update(string key, string? value)
        {
            Set(key, value);
            OnReload();
        }
    }
}
