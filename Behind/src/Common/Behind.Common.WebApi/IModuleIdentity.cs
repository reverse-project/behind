using Microsoft.AspNetCore.Http;

namespace Behind.Common.WebApi;

public interface IModuleIdentity
{
    Task<Guid?> UserId(HttpContext httpContext);
}
