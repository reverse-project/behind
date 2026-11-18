using System.ComponentModel;
using System.Text.Json.Serialization;
using Behind.Common;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Common.Converters;

namespace Behind.Core.Common.Configuration;

public sealed record DatabaseConfiguration(Option<Provider> Provider, Option<DatabaseKey> Key)
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

[TypeConverter(typeof(DatabaseKeyTypeConverter))]
[JsonConverter(typeof(DatabaseKeyJsonConverter))]
public sealed record DatabaseKey(string Value)
{
    public static implicit operator string(DatabaseKey key) => key.Value;
    
    public override string ToString() => Value;
}