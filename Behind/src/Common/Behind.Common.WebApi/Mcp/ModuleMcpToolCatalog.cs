using Behind.Common.Modules;
using Behind.Common.WebApi.Middleware;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Options;

namespace Behind.Common.WebApi.Mcp;

public sealed class ModuleMcpToolCatalog(
    EndpointDataSource endpoints,
    IApiDescriptionGroupCollectionProvider descriptions,
    ModuleCatalog modules,
    ModuleAvailability availability,
    IOptions<JsonOptions> serialization) : IModuleMcpToolCatalog
{
    public IReadOnlyList<ModuleMcpTool> List()
    {
        var installed = modules.Modules.Select(module => module.Key).ToHashSet(StringComparer.Ordinal);
        var apis = descriptions.ApiDescriptionGroups.Items.SelectMany(group => group.Items).ToArray();
        var tools = new List<ModuleMcpTool>();
        foreach (var endpoint in endpoints.Endpoints.OfType<RouteEndpoint>())
        {
            var module = endpoint.Metadata.GetMetadata<ModuleEndpoint>();
            if (module is null || !installed.Contains(module.Key) || !availability.IsEnabled(module.Key)
                || endpoint.Metadata.GetMetadata<HttpMethodMetadata>() is not { } methods)
            {
                continue;
            }

            var responses = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();
            if (!responses.Any(response => response.StatusCode is >= 200 and < 300
                && (response.StatusCode == 204 || response.ContentTypes.Any(IsJson))))
            {
                continue;
            }

            var path = "/" + string.Join("/", endpoint.RoutePattern.PathSegments.Select(segment =>
                string.Concat(segment.Parts.Select(part => part switch
                {
                    RoutePatternLiteralPart literal => literal.Content,
                    RoutePatternParameterPart parameter => "{" + parameter.Name + "}",
                    RoutePatternSeparatorPart separator => separator.Content,
                    _ => throw new InvalidOperationException("Unsupported module route part.")
                }))));
            foreach (var method in methods.HttpMethods.Where(method => method is "GET" or "POST" or "PUT" or "PATCH" or "DELETE"))
            {
                var api = apis.SingleOrDefault(api => api.HttpMethod == method
                    && (api.RelativePath?.TrimStart('/') == path.TrimStart('/')
                        || api.RelativePath?.TrimStart('/') == endpoint.RoutePattern.RawText?.TrimStart('/')));
                if (api is null || api.ParameterDescriptions.Any(parameter =>
                    parameter.Source != BindingSource.Path && parameter.Source != BindingSource.Query
                    && parameter.Source != BindingSource.Body)
                    || api.SupportedRequestFormats.Any(format => !IsJson(format.MediaType)))
                {
                    continue;
                }

                var nameParts = endpoint.RoutePattern.PathSegments.Select(segment =>
                    string.Join("_", segment.Parts.Select(part => part is RoutePatternParameterPart parameter
                        ? "by_" + parameter.Name
                        : part is RoutePatternLiteralPart literal ? literal.Content : ""))).ToArray();
                var routeName = string.Join("_", nameParts.Skip(nameParts.FirstOrDefault() == module.Key ? 1 : 0));
                var name = string.Join("_", new[] { module.Key, method, routeName }.Where(part => part.Length > 0));
                name = string.Concat(name.ToLowerInvariant().Select(character =>
                    char.IsAsciiLetterOrDigit(character) || character == '_' ? character : '_'));
                tools.Add(new ModuleMcpTool(name,
                    endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary ?? $"{method} {path}",
                    method, path, endpoint.RoutePattern.Parameters, api.ParameterDescriptions.ToArray(),
                    serialization.Value.SerializerOptions));
            }
        }

        if (tools.GroupBy(tool => tool.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Module MCP tool names must be unique.");
        }
        return tools.OrderBy(tool => tool.Name, StringComparer.Ordinal).ToArray();
    }

    private static bool IsJson(string contentType) =>
        contentType.Split(';')[0].Trim() is "application/json" or "application/problem+json";
}
