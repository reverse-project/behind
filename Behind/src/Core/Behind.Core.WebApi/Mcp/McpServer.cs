using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Mcp;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Behind.Core.WebApi.Mcp;

public sealed class McpServer(
    IMcpRequestDispatcher dispatcher,
    IModuleMcpToolCatalog moduleTools,
    IOptionsMonitor<McpConfiguration> configuration,
    ILogger<McpServer> logger) : IMcpServer
{
    public const string ProtocolVersion = "2025-11-25";
    private static readonly string[] SupportedVersions = [ProtocolVersion, "2025-06-18", "2025-03-26"];

    public async Task<IResult> Handle(HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.HasJsonContentType())
        {
            return TypedResults.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        if (!MediaTypeHeaderValue.TryParseList(context.Request.Headers.Accept.OfType<string>().ToArray(), out var accept)
            || !accept.Any(item => item.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                && item.Quality.GetValueOrDefault(1) > 0)
            || !accept.Any(item => item.MediaType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase)
                && item.Quality.GetValueOrDefault(1) > 0))
        {
            return TypedResults.StatusCode(StatusCodes.Status406NotAcceptable);
        }

        var header = context.Request.Headers["MCP-Protocol-Version"];
        if (header.Count > 1 || header.Count == 1 && !SupportedVersions.Contains(header.ToString(), StringComparer.Ordinal))
        {
            return Error(default, -32600, "Unsupported MCP protocol version.", StatusCodes.Status400BadRequest);
        }

        var maximumBytes = configuration.CurrentValue.MaxRequestBytes;
        if (context.Request.ContentLength > maximumBytes)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        await using var input = new SizeLimitedBuffer(maximumBytes);
        try
        {
            await context.Request.Body.CopyToAsync(input, cancellationToken);
        }
        catch (PayloadLimitExceededException)
        {
            logger.LogWarning("Rejected MCP request exceeding the configured request size");
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        input.Position = 0;
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(input, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            logger.LogWarning("Rejected malformed MCP JSON");
            return Error(default, -32700, "Parse error.", StatusCodes.Status400BadRequest);
        }

        using (document)
        {
            var message = document.RootElement;
            if (message.ValueKind != JsonValueKind.Object || HasDuplicateProperties(message)
                || !message.TryGetProperty("jsonrpc", out var version) || version.ValueKind != JsonValueKind.String
                || version.GetString() != "2.0"
                || !message.TryGetProperty("method", out var method) || method.ValueKind != JsonValueKind.String
                || message.TryGetProperty("params", out var parameters) && parameters.ValueKind != JsonValueKind.Object)
            {
                return Error(default, -32600, "Invalid request.", StatusCodes.Status400BadRequest);
            }

            var hasId = message.TryGetProperty("id", out var id);
            if (hasId && id.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
            {
                return Error(default, -32600, "Invalid request id.", StatusCodes.Status400BadRequest);
            }

            var name = method.GetString();
            if (!hasId)
            {
                return name?.StartsWith("notifications/", StringComparison.Ordinal) == true
                    ? TypedResults.Accepted((string?)null)
                    : Error(default, -32600, "A request id is required.", StatusCodes.Status400BadRequest);
            }

            switch (name)
            {
                case "initialize":
                    if (parameters.ValueKind != JsonValueKind.Object
                        || !parameters.TryGetProperty("protocolVersion", out var requested)
                        || requested.ValueKind != JsonValueKind.String
                        || !parameters.TryGetProperty("capabilities", out var capabilities)
                        || capabilities.ValueKind != JsonValueKind.Object
                        || !parameters.TryGetProperty("clientInfo", out var client)
                        || client.ValueKind != JsonValueKind.Object
                        || !client.TryGetProperty("name", out var clientName) || clientName.ValueKind != JsonValueKind.String
                        || !client.TryGetProperty("version", out var clientVersion) || clientVersion.ValueKind != JsonValueKind.String)
                    {
                        return Error(id, -32602, "Invalid initialization parameters.");
                    }
                    return Success(id, new JsonObject
                    {
                        ["protocolVersion"] = SupportedVersions.Contains(requested.GetString(), StringComparer.Ordinal)
                            ? requested.GetString() : ProtocolVersion,
                        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                        ["serverInfo"] = new JsonObject { ["name"] = AboutResponse.ProductName, ["version"] = AboutResponse.Current.Version },
                        ["instructions"] = "Use tools/list to discover dedicated module operations, or api_schema and api_request for application JSON APIs. "
                            + "Operations run with the service user's own current roles and grants, not its creator's. "
                            + "Interactive accounts, credentials and binary transfers are not available. Mutations are not automatically retried."
                    });
                case "ping":
                    return Success(id, new JsonObject());
                case "tools/list":
                    var tools = Tools();
                    foreach (var tool in AvailableModuleTools())
                    {
                        tools["tools"]!.AsArray().Add(tool.Definition());
                    }
                    return Success(id, tools);
                case "tools/call":
                    return await Call(context, id, parameters, cancellationToken);
                default:
                    return Error(id, -32601, "Method not found.");
            }
        }
    }

    private async Task<IResult> Call(
        HttpContext context, JsonElement id, JsonElement parameters, CancellationToken cancellationToken)
    {
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
        {
            return Error(id, -32602, "A tool name is required.");
        }

        JsonElement arguments;
        using var schemaArguments = JsonDocument.Parse("""{"method":"GET","path":"/openapi/v1.json"}""");
        if (name.GetString() == "api_schema")
        {
            if (parameters.TryGetProperty("arguments", out var supplied)
                && (supplied.ValueKind != JsonValueKind.Object || supplied.EnumerateObject().Any()))
            {
                return Error(id, -32602, "api_schema takes no arguments.");
            }
            arguments = schemaArguments.RootElement;
        }
        else if (name.GetString() == "api_request")
        {
            if (!parameters.TryGetProperty("arguments", out arguments) || !ValidArguments(arguments))
            {
                return Error(id, -32602, "Expected method, local path, optional string-valued query and JSON body.");
            }
        }
        else
        {
            var tool = AvailableModuleTools().SingleOrDefault(tool => tool.Name == name.GetString());
            if (tool is null)
            {
                return Error(id, -32602, "Unknown or disabled tool.");
            }
            using var empty = JsonDocument.Parse("{}");
            var bound = tool.Bind(parameters.TryGetProperty("arguments", out var supplied) ? supplied : empty.RootElement);
            return await bound.Match(
                () => Task.FromResult(Error(id, -32602, "Invalid module tool arguments; use the tool's input schema.")),
                async request =>
                {
                    using var document = JsonDocument.Parse(request.ToJsonString());
                    return ToolResult(id, await dispatcher.Dispatch(context, document.RootElement, cancellationToken));
                });
        }

        var response = await dispatcher.Dispatch(context, arguments, cancellationToken);
        return ToolResult(id, response);
    }

    private IEnumerable<ModuleMcpTool> AvailableModuleTools() =>
        moduleTools.List().Where(tool => McpRequestDispatcher.IsAllowedPath(tool.Path));

    private static IResult ToolResult(JsonElement id, McpApiResponse response) =>
        Success(id, new JsonObject
        {
            ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "text",
                ["text"] = JsonSerializer.Serialize(new { status = response.Status, body = response.Body })
            }),
            ["isError"] = response.Status >= 400
        });

    private static bool ValidArguments(JsonElement arguments) =>
        arguments.ValueKind == JsonValueKind.Object
        && arguments.EnumerateObject().All(item => item.Name is "method" or "path" or "query" or "body")
        && arguments.TryGetProperty("method", out var method) && method.ValueKind == JsonValueKind.String
        && method.GetString() is "GET" or "POST" or "PUT" or "PATCH" or "DELETE"
        && arguments.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(path.GetString())
        && (!arguments.TryGetProperty("query", out var query)
            || query.ValueKind == JsonValueKind.Object
            && query.EnumerateObject().All(item => item.Value.ValueKind == JsonValueKind.String));

    private static bool HasDuplicateProperties(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Object => value.EnumerateObject().GroupBy(item => item.Name, StringComparer.Ordinal)
                .Any(group => group.Count() > 1) || value.EnumerateObject().Any(item => HasDuplicateProperties(item.Value)),
            JsonValueKind.Array => value.EnumerateArray().Any(HasDuplicateProperties),
            _ => false
        };

    private static JsonObject Tools() => JsonNode.Parse("""
        {
          "tools": [
            {
              "name": "api_schema",
              "description": "Discover JSON application operations and their request shapes using OpenAPI. Interactive authentication and binary transfers are unavailable.",
              "inputSchema": { "type": "object", "properties": {}, "additionalProperties": false }
            },
            {
              "name": "api_request",
              "description": "Execute a JSON application API operation with the service user's own current permissions. Mutations execute once and are not cached.",
              "inputSchema": {
                "type": "object",
                "properties": {
                  "method": { "type": "string", "enum": ["GET", "POST", "PUT", "PATCH", "DELETE"] },
                  "path": { "type": "string", "description": "Local API path, with route parameters substituted; no query string." },
                  "query": { "type": "object", "additionalProperties": { "type": "string" } },
                  "body": {}
                },
                "required": ["method", "path"],
                "additionalProperties": false
              }
            }
          ]
        }
        """)!.AsObject();

    private static IResult Success(JsonElement id, JsonObject result) =>
        TypedResults.Json(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = JsonNode.Parse(id.GetRawText()), ["result"] = result });

    private static IResult Error(JsonElement id, int code, string message, int status = StatusCodes.Status200OK) =>
        TypedResults.Json(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.ValueKind == JsonValueKind.Undefined ? null : JsonNode.Parse(id.GetRawText()),
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
        }, statusCode: status);
}
