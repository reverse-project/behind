using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Mcp;

public interface IMcpServer
{
    Task<IResult> Handle(HttpContext context, CancellationToken cancellationToken);
}
