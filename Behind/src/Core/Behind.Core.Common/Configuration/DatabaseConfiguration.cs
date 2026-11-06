using System.ComponentModel;
using System.Text.Json.Serialization;
using Behind.Common;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Common.Converters;

namespace Behind.Core.Common.Configuration;

public sealed record DatabaseConfiguration(Option<Provider> Provider, Option<Key> Key)
{
    public DatabaseConfiguration() : this(None, None)
    {
        
    }
    
    public const string SectionName = "Database";
}

[TypeConverter(typeof(ProviderTypeConverter))]
[JsonConverter(typeof(ProviderJsonConverter))]
public abstract record Provider(string Value)
{
    public static implicit operator string(Provider provider) => provider.Value;
    
    public override string ToString() => Value;
}

public sealed record Sqlite() : Provider(nameof(Sqlite));

public sealed record Postgres() : Provider(nameof(Postgres));

[TypeConverter(typeof(KeyTypeConverter))]
[JsonConverter(typeof(KeyJsonConverter))]
public sealed record Key(string Value)
{
    public static implicit operator string(Key key) => key.Value;
    
    public override string ToString() => Value;
}