using System.Text.Json.Serialization;

namespace Behind.Core.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<MagicLinkFunction>))]
public enum MagicLinkFunction
{
    SignIn,
    LostPassword
}
