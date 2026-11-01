using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Core.WebApi.Authorization;

public static class GrantAuthorizationExtensions
{
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
