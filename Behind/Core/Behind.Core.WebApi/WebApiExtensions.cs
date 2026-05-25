using System.Text;
using Behind.Common.WebApi.Filters;
using Behind.Common.WebApi.Handlers;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Behind.Core.WebApi;

public static class WebApiExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddCore()
        {
            builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
            builder.Services.AddOpenApi();
            builder.Services.AddTransient<IAuditLogEntryQueries, AuditLogEntryQueries>();
            builder.Services.AddDbContext<AbstractCoreContext, CoreContextSqlite>(options =>
                options.UseSqlite("Data Source=/workspace/Data/core.db")
            );

            builder.Configuration.AddJsonFile("security.json", true, true);
            builder.Services.Configure<SecurityConfiguration>(
                builder.Configuration.GetSection(SecurityConfiguration.SectionName)
            );

            builder.Configuration.AddJsonFile("frontend.json", true, true);
            builder.Services.Configure<FrontendConfiguration>(
                builder.Configuration.GetSection(FrontendConfiguration.SectionName)
            );

            builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, DomainPasswordHasher<ApplicationUser>>();
            builder.Services.AddSingleton<IUserStore<ApplicationUser>, InMemoryUserStore>();
            builder.Services.AddSingleton<IRoleStore<ApplicationRole>, InMemoryRoleStore>();

            builder.Services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireDigit = false;
                    options.Password.RequireLowercase = false;
                    options.Password.RequireUppercase = false;
                })
                .AddRoles<ApplicationRole>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            var securityConfiguration = builder.Configuration.GetSection(SecurityConfiguration.SectionName)
                .Get<SecurityConfiguration>();

            if (securityConfiguration is null or { Jwt: null })
            {
                return builder;
            }
            
            var signingKey = Encoding.UTF8.GetBytes(securityConfiguration.Jwt.SigningKey);

            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = securityConfiguration.Jwt.Issuer,
                        ValidAudience = securityConfiguration.Jwt.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(signingKey)
                    };
                });

            builder.Services.AddAuthorization();

            return builder;
        }
    }

    extension(WebApplication application)
    {
        public WebApplication UseCore()
        {
            application.UseExceptionHandler(_ => { });
            application.MapOpenApi();

            application.MapGet("/config", ConfigEndpoint.ExecuteGet);
            application.MapGet("/auditLog", AuditLogEntryEndpoints.Get)
                .Produces<IEnumerable<AuditLogEntry>>()
                .Produces(204)
                .Produces(400)
                .Produces(500);
            
            application.UseAuthentication();
            application.UseAuthorization();

            application.MapPost("/sign/up/password", SignUpEndpoints.SignUpPassword)
                .AddEndpointFilter<DataAnnotationValidationFilter<SignUpEndpoints.PasswordRequest>>()
                .Produces(204)
                .Produces(400)
                .Produces(409)
                .Produces(500);
            
            application.MapPost("/sign/in/password", SignInEndpoints.SignInPassword)
                .AddEndpointFilter<DataAnnotationValidationFilter<SignInEndpoints.PasswordRequest>>()
                .Produces(200)
                .Produces(400)
                .Produces(500);

            return application;
        }
    }
}