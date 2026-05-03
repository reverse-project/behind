using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace Behind.Common.WebApi;

public static class ConfigurationExtensions
{
    extension(IConfiguration config)
    {
        public JsonNode Serialize()
        {
            var children = config.GetChildren().ToList();

            if (children.Count == 0)
            {
                return JsonValue.Create(((IConfigurationSection)config).Value) ?? JsonValue.Create(string.Empty);
            }

            bool isArray = children.All(c => int.TryParse(c.Key, out _));

            if (isArray)
            {
                var jsonArray = new JsonArray();
                foreach (var child in children.OrderBy(c => int.Parse(c.Key)))
                {
                    jsonArray.Add(child.Serialize());
                }

                return jsonArray;
            }

            var jsonObject = new JsonObject();
            foreach (var child in children)
            {
                jsonObject[child.Key] = child.Serialize();
            }

            return jsonObject;
        }
    }
}