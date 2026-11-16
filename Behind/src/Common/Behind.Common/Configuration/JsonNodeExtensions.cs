using System.Text.Json.Nodes;

namespace Behind.Common.Configuration;

public static class JsonNodeExtensions
{
    extension(JsonNode node)
    {
        public JsonNode WithoutNulls()
        {
            switch (node)
            {
                case JsonObject container:
                    foreach (var key in container.Where(pair => pair.Value is null).Select(pair => pair.Key).ToList())
                    {
                        container.Remove(key);
                    }

                    foreach (var child in container.Select(pair => pair.Value))
                    {
                        child?.WithoutNulls();
                    }

                    break;
                case JsonArray container:
                    foreach (var item in container)
                    {
                        item?.WithoutNulls();
                    }

                    break;
            }

            return node;
        }
    }
}
