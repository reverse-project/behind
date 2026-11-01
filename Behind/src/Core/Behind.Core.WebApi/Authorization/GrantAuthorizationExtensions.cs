using System.Security.Claims;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Core.WebApi.Authorization;

public static class GrantAuthorizationExtensions
{
    extension(IAuthorizationService authorization)
    {
        public async Task<bool> HoldsGrantAsync(ClaimsPrincipal user, string grant) =>
            (await authorization.AuthorizeAsync(user, grant)).Succeeded;
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddGrantAuthorization()
        {
            services.AddScoped<IAuthorizationHandler, GrantAuthorizationHandler>();

            return services.AddAuthorization(options =>
            {
                foreach (var grant in BuiltInAccess.Grants)
                {
                    options.AddPolicy(
                        grant.Grant.Name,
                        policy => policy
                            .RequireAuthenticatedUser()
                            .AddRequirements(new GrantRequirement(grant.Grant.Name)));
                }
            });
        }
    }
}
