using Behind.Core.Database;
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

            services.AddAuthorization();
            services.AddOptions<AuthorizationOptions>()
                .Configure<AccessCatalog>((options, catalog) =>
                {
                    foreach (var grant in catalog.Grants)
                    {
                        options.AddPolicy(
                            grant.Grant.Name,
                            policy => policy
                                .RequireAuthenticatedUser()
                                .AddRequirements(new GrantRequirement(grant.Grant.Name)));
                    }
                });

            return services.AddAccessCatalog();
        }
    }
}
