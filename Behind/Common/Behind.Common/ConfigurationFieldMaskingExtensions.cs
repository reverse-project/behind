namespace Behind.Common;

using System.Text.Json.Nodes;

public static class ConfigurationFieldMaskingExtensions
{
    public static JsonNode? MaskFields(this JsonNode? node, string sectionName, params string[] fieldNames)
    {
        if (node is not JsonObject root)
            return node;

        if (root[sectionName] is JsonObject section)
            MaskFieldsInObject(section, fieldNames);

        return node;
    }

    public static JsonNode? MaskNestedFields(this JsonNode? node, string[] sectionPath, params string[] fieldNames)
    {
        if (node is not JsonObject root)
            return node;

        var current = (JsonNode?)root;
        foreach (var section in sectionPath[..^1])
        {
            if (current is JsonObject obj && obj[section] is JsonNode next)
            {
                current = next;
            }
            else
            {
                return node;
            }
        }

        if (current is JsonObject parentObj && parentObj[sectionPath[^1]] is JsonObject targetSection)
            MaskFieldsInObject(targetSection, fieldNames);

        return node;
    }

    private static void MaskFieldsInObject(JsonObject obj, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (obj[fieldName] is JsonValue jsonValue)
            {
                var stringValue = jsonValue.GetValue<object>()?.ToString();
                if (stringValue is not null)
                {
                    obj[fieldName] = stringValue.MaskSensitiveValue();
                }
            }
        }
    }
}
