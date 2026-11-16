using System.ComponentModel;
using System.Text.Json;
using Behind.Common.Configuration;

namespace Behind.Tests.Common;

public sealed class CachingProviderTests
{
    [Theory]
    [InlineData("Memory")]
    [InlineData("RedisCompatible")]
    [InlineData("memory")]
    [InlineData("rediscompatible")]
    public void ProviderRoundTripsThroughTypeDescriptorAndJson(string name)
    {
        var converter = TypeDescriptor.GetConverter(typeof(CachingProvider));
        var provider = Assert.IsAssignableFrom<CachingProvider>(converter.ConvertFromInvariantString(name));
        var canonical = provider is Memory ? "Memory" : "RedisCompatible";
        Assert.Equal(canonical, converter.ConvertToInvariantString(provider));
        Assert.Equal(canonical, provider.ToString());
        Assert.Equal($"\"{canonical}\"", JsonSerializer.Serialize(provider));
        Assert.Equal(provider, JsonSerializer.Deserialize<CachingProvider>($"\"{name}\""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("999")]
    public void InvalidProvidersAreRejected(string value)
    {
        Assert.Throws<FormatException>(() =>
            TypeDescriptor.GetConverter(typeof(CachingProvider)).ConvertFromInvariantString(value));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CachingProvider>($"\"{value}\""));
    }

    [Fact]
    public void KeyRoundTripsAndConfigurationHasOnlyProviderAndKey()
    {
        var converter = TypeDescriptor.GetConverter(typeof(CachingKey));
        var key = Assert.IsType<CachingKey>(converter.ConvertFromInvariantString("Valkey"));
        Assert.Equal("Valkey", converter.ConvertToInvariantString(key));
        Assert.Equal("\"Valkey\"", JsonSerializer.Serialize(key));
        Assert.Equal(key, JsonSerializer.Deserialize<CachingKey>("\"Valkey\""));
        Assert.Equal(new[] { "Key", "Provider" }, typeof(CachingConfiguration)
            .GetProperties().Select(property => property.Name).Order());
    }

    [Fact]
    public void ConfigurationDefaultsToMemory()
    {
        Assert.IsType<Memory>(new CachingConfiguration().Provider);
        Assert.IsType<Memory>(JsonSerializer.Deserialize<CachingConfiguration>("{}")!.Provider);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CachingProvider>("42"));
    }
}
