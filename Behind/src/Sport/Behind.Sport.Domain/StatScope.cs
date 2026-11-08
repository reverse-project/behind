using System.Text.Json.Serialization;

namespace Behind.Sport.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<StatScope>))]
public enum StatScope
{
    Player,
    Team
}
