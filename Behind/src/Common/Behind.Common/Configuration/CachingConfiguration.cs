using System.ComponentModel;
using System.Text.Json.Serialization;
using Behind.Common.Converters;

namespace Behind.Common.Configuration;

public sealed record CachingConfiguration
{
    public const string SectionName = "Caching";

    public CachingProvider Provider { get; init; } = new Memory();
    public Option<CachingKey> Key { get; init; }
}

[TypeConverter(typeof(CachingKeyTypeConverter))]
[JsonConverter(typeof(CachingKeyJsonConverterFactory))]
public sealed record CachingKey(string Value)
{
    public static implicit operator string(CachingKey key) => key.Value;

    public override string ToString() => Value;
}

[TypeConverter(typeof(CachingProviderTypeConverter))]
[JsonConverter(typeof(CachingProviderJsonConverterFactory))]
public abstract record CachingProvider(string Value)
{
    public static implicit operator string(CachingProvider provider) => provider.Value;

    public sealed override string ToString() => Value;
}

public sealed record Memory() : CachingProvider(nameof(Memory));

public sealed record RedisCompatible() : CachingProvider(nameof(RedisCompatible));
