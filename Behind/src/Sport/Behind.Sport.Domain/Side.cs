using System.Text.Json.Serialization;

namespace Behind.Sport.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<Side>))]
public enum Side
{
    Left,
    Right,
    Both
}
