using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Identity;

public sealed class ModuleIdentity(ISessionQueries sessions) : IModuleIdentity
{
    public async Task<Guid?> UserId(HttpContext httpContext) =>
        (await httpContext.FindCurrentSession(sessions)).Match<Guid?>(() => null, session => session.UserId);
}
