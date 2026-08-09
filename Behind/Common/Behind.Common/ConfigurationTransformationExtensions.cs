namespace Behind.Common;

using System.Text.Json.Nodes;

public static class ConfigurationTransformationExtensions
{
    public static JsonNode? MaskSensitiveConfigurationValues(this JsonNode? node, string sectionName)
    {
        if (node is not JsonObject root)
            return node;

        if (root[sectionName] is JsonObject sensitiveSection)
        {
            foreach (var key in sensitiveSection.AsEnumerable().Select(kvp => kvp.Key).ToList())
            {
                if (sensitiveSection[key] is JsonValue stringValue && stringValue.GetValue<string>() is string value)
                {
                    sensitiveSection[key] = value.MaskSensitiveValue();
                }
            }
        }

        return node;
    }
}
