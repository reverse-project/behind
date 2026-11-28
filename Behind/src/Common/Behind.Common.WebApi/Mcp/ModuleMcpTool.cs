using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing.Patterns;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.WebApi.Mcp;

public sealed record ModuleMcpTool(
    string Name,
    string Description,
    string Method,
    string Path,
    IReadOnlyList<RoutePatternParameterPart> RouteParameters,
    IReadOnlyList<ApiParameterDescription> Parameters,
    JsonSerializerOptions Serialization)
{
    public JsonObject Definition()
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var source in new[] { BindingSource.Path, BindingSource.Query })
        {
            var parameters = Parameters.Where(parameter => parameter.Source == source).ToArray();
            if (parameters.Length == 0)
            {
                continue;
            }
            var group = source == BindingSource.Path ? "route" : "query";
            var fields = new JsonObject();
            var requiredFields = new JsonArray();
            foreach (var parameter in parameters)
            {
                var schema = source == BindingSource.Path
                    ? new JsonObject { ["type"] = "string", ["description"] = $"Route parameter {parameter.Name} ({parameter.Type.Name})." }
                    : Serialization.GetJsonSchemaAsNode(Nullable.GetUnderlyingType(parameter.Type) ?? parameter.Type);
                RebaseReferences(schema, $"/properties/{group}/properties/{Pointer(parameter.Name)}");
                fields[parameter.Name] = schema;
                if (Required(parameter))
                {
                    requiredFields.Add(parameter.Name);
                }
            }
            properties[group] = new JsonObject
            {
                ["type"] = "object", ["properties"] = fields,
                ["required"] = requiredFields, ["additionalProperties"] = false
            };
            if (requiredFields.Count > 0)
            {
                required.Add(group);
            }
        }
        foreach (var body in Parameters.Where(parameter => parameter.Source == BindingSource.Body))
        {
            var schema = Serialization.GetJsonSchemaAsNode(body.Type);
            RebaseReferences(schema, "/properties/body");
            properties["body"] = schema;
            if (body.IsRequired)
            {
                required.Add("body");
            }
        }
        return new JsonObject
        {
            ["name"] = Name,
            ["description"] = Description + " Runs with the service user's current permissions; mutations execute once.",
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object", ["properties"] = properties,
                ["required"] = required, ["additionalProperties"] = false
            },
            ["annotations"] = new JsonObject { ["readOnlyHint"] = Method == "GET" }
        };
    }

    public Option<JsonObject> Bind(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || arguments.EnumerateObject().Any(property => property.Name is not ("route" or "query" or "body")))
        {
            return None;
        }
        var path = Path;
        var query = new JsonObject();
        foreach (var source in new[] { BindingSource.Path, BindingSource.Query })
        {
            var group = source == BindingSource.Path ? "route" : "query";
            var parameters = Parameters.Where(parameter => parameter.Source == source).ToArray();
            var present = arguments.TryGetProperty(group, out var supplied);
            if (present && (parameters.Length == 0 || supplied.ValueKind != JsonValueKind.Object
                || supplied.EnumerateObject().Any(property => !parameters.Any(parameter => parameter.Name == property.Name))))
            {
                return None;
            }
            foreach (var parameter in parameters)
            {
                if (!present || !supplied.TryGetProperty(parameter.Name, out var value))
                {
                    if (Required(parameter))
                    {
                        return None;
                    }
                    if (source == BindingSource.Path)
                    {
                        var route = RouteParameters.Single(part => part.Name == parameter.Name);
                        path = path.Replace("{" + parameter.Name + "}",
                            Convert.ToString(route.Default, System.Globalization.CultureInfo.InvariantCulture) ?? "",
                            StringComparison.Ordinal).TrimEnd('/');
                    }
                    continue;
                }
                if (source == BindingSource.Path)
                {
                    if (value.ValueKind != JsonValueKind.String)
                    {
                        return None;
                    }
                    var text = value.GetString()!;
                    var route = RouteParameters.Single(part => part.Name == parameter.Name);
                    if (!route.IsCatchAll && (text.Length == 0 || text.Contains('/'))
                        || text.Contains('\\') || text.Contains('%') || text.Contains('?') || text.Contains('#')
                        || text.Contains('{') || text.Contains('}')
                        || text.Split('/').Any(segment => segment is "." or ".."))
                    {
                        return None;
                    }
                    path = path.Replace("{" + parameter.Name + "}", text, StringComparison.Ordinal);
                }
                else
                {
                    if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
                    {
                        return None;
                    }
                    query[parameter.Name] = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
                }
            }
        }
        var bodyParameter = Parameters.SingleOrDefault(parameter => parameter.Source == BindingSource.Body);
        var hasBody = arguments.TryGetProperty("body", out var body);
        if (hasBody && bodyParameter is null || !hasBody && bodyParameter?.IsRequired == true
            || path.Contains('{') || path.Contains('}'))
        {
            return None;
        }
        var request = new JsonObject { ["method"] = Method, ["path"] = path, ["query"] = query };
        if (hasBody)
        {
            request["body"] = JsonNode.Parse(body.GetRawText());
        }
        return request;
    }

    private bool Required(ApiParameterDescription parameter) =>
        parameter.Source == BindingSource.Path
            ? RouteParameters.Any(part => part.Name == parameter.Name && !part.IsOptional && part.Default is null)
            : parameter.IsRequired;

    private static string Pointer(string value) => value.Replace("~", "~0").Replace("/", "~1");

    private static void RebaseReferences(JsonNode schema, string prefix)
    {
        if (schema is JsonObject properties)
        {
            if (properties["$ref"] is JsonValue reference && reference.TryGetValue<string>(out var target)
                && target.StartsWith('#'))
            {
                properties["$ref"] = "#" + prefix + target[1..];
            }
            foreach (var child in properties.Select(property => property.Value).OfType<JsonNode>().ToArray())
            {
                RebaseReferences(child, prefix);
            }
        }
        else if (schema is JsonArray items)
        {
            foreach (var child in items.OfType<JsonNode>())
            {
                RebaseReferences(child, prefix);
            }
        }
    }
}
