using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Behind.Common.WebApi;

public static class AuthorizationServiceExtensions
{
    extension(IAuthorizationService authorization)
    {
        public async Task<bool> HoldsGrantAsync(ClaimsPrincipal user, string grant) =>
            (await authorization.AuthorizeAsync(user, grant)).Succeeded;
    }
}
