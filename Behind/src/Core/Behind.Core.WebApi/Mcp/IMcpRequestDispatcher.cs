using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Mcp;

public interface IMcpRequestDispatcher
{
    Task<McpApiResponse> Dispatch(HttpContext caller, JsonElement arguments, CancellationToken cancellationToken);
}

/// <summary>The status and text returned by an authorized application API request.</summary>
public sealed record McpApiResponse(int Status, string Body);
