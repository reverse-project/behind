using System.Text.Json;
using System.Text.Json.Serialization;
using Behind.Core.Common.Configuration;

namespace Behind.Core.Common.Converters;

public sealed class EmailAuthenticationJsonConverter : JsonConverter<Authentication>
{
    private const string TypeProperty = nameof(Authentication.Type);
    private const string UsernameProperty = nameof(BasicAuthentication.Username);
    private const string PasswordProperty = nameof(BasicAuthentication.Password);

    private static readonly EmailAuthenticationTypeConverter TypeConverter = new();

    public override Authentication? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() is { } type ? (Authentication?)TypeConverter.ConvertFrom(type) : null,
            JsonTokenType.StartObject => ReadObject(ref reader),
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, Authentication value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(TypeProperty, value.Type);

        if (value is BasicAuthentication basic)
        {
            writer.WriteString(UsernameProperty, basic.Username);
            writer.WriteString(PasswordProperty, basic.Password);
        }

        writer.WriteEndObject();
    }

    private static Authentication? ReadObject(ref Utf8JsonReader reader)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return root.TryGetProperty(TypeProperty, out var type) && type.GetString() is { } kind
            ? TypeConverter.ConvertFrom(kind) switch
            {
                BasicAuthentication => new BasicAuthentication(Text(root, UsernameProperty), Text(root, PasswordProperty)),
                var converted => (Authentication?)converted
            }
            : null;
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;
}
