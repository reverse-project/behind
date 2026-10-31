using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Behind.Common.Configuration;
using Behind.Common.Converters;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Behind.Common.WebApi.Handlers;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Behind.Common.WebApi.Middleware;
using Behind.Common.WebApi.Security;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Converters;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Core.Domain;
using Behind.Core.WebApi.Auditing;
using Behind.Core.WebApi.EmailSender;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.MagicLinks;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi;

[ExcludeFromCodeCoverage]
public static class WebApiExtensions
{
    extension(WebApplicationBuilder builder)
    {
        WebApplicationBuilder AddTypeDescriptors()
        {
            OptionTypeConverterRegistration.Register<SecretKey>();
            OptionTypeConverterRegistration.Register<Uuid>();
            OptionTypeConverterRegistration.Register<JwtSettings>();
            OptionTypeConverterRegistration.Register<MagicLinkSettings>();
            OptionTypeConverterRegistration.Register<DataProtectionSettings>();
            TypeDescriptor.AddAttributes(
                typeof(AuditLogEntry.AuditAction),
                new TypeConverterAttribute(typeof(AuditLogEntryAuditActionTypeConverter)));

            return builder;
        }

        WebApplicationBuilder AddDatabase()
        {
            builder.Configuration.AddJsonFile("database.json", false, true);
            
            DatabaseConfiguration databaseConfiguration = builder.Configuration
                .GetSection(DatabaseConfiguration.SectionName)
                .GetOptionAware<DatabaseConfiguration>() ?? new DatabaseConfiguration();

            builder.Services.AddSingleton(
                Options.Create(databaseConfiguration)
            );
            
            databaseConfiguration.RegisterSqlite(builder);

            return builder;
        }

        WebApplicationBuilder AddEmail()
        {
            builder.Configuration.AddJsonFile("email.json", false, true);

            EmailConfiguration emailConfiguration = builder.Configuration
                .GetSection(EmailConfiguration.SectionName)
                .GetOptionAware<EmailConfiguration>() ?? new LoggerEmailConfiguration();

            builder.Services.AddSingleton(
                Options.Create(emailConfiguration)
            );

            builder.Services.AddTransient<IEmailSender>(emailConfiguration.GetInstance);
            builder.Services.AddTransient<IEmailSender<ApplicationUser>>(emailConfiguration.GetInstance);
            builder.Services.AddTransient<IMagicLinkEmailSender>(emailConfiguration.GetInstance);

            return builder;
        }

        WebApplicationBuilder AddSecurity()
        {
            builder.Configuration.AddJsonFile("security.json", true, true);

            SecurityConfiguration securityConfiguration = builder.Configuration
                .GetSection(SecurityConfiguration.SectionName)
                .GetOptionAware<SecurityConfiguration>() ?? new SecurityConfiguration();

            builder.Services.AddSingleton(
                Options.Create(securityConfiguration)
            );

            builder.Services.AddMagicLink(
                securityConfiguration,
                new FileInfo(Path.Combine(builder.Environment.ContentRootPath, "security.json"))
            );

            builder.Configuration.AddJsonFile("frontend.json", true, true);
            builder.Services.Configure<FrontendConfiguration>(
                builder.Configuration.GetSection(FrontendConfiguration.SectionName)
            );

            securityConfiguration.DataProtection.Match(
                none: () => builder,
                settings =>
                {
                    builder.Services.AddCertificateProtectedDataProtection(
                        builder.Environment.ApplicationName,
                        settings.KeysPath,
                        settings.CertificatePath,
                        settings.CertificatePassword
                    );
                    return builder;
                });

            builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, DomainPasswordHasher<ApplicationUser>>();
            builder.Services.AddScoped<IUserStore<ApplicationUser>, IdentityStore>();
            builder.Services.AddScoped<IRoleStore<ApplicationRole>, IdentityStore>();

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

            return securityConfiguration.Jwt.Match(
                none: () => builder,
                jwtSettings =>
                {
                    builder.Services.AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                        })
                        .AddJwtBearer(options =>
                        {
                            options.TokenValidationParameters = jwtSettings.ValidationParameters;
                        });

                    builder.Services.AddAuthorization();
                    return builder;
                });
        }

        WebApplicationBuilder AddServices()
        {
            builder.Services.AddScoped<IAuditLogEntryQueries, AuditLogEntryQueries>();
            builder.Services.AddScoped<IUserQueries, UserQueries>();
            builder.Services.AddScoped<IEmailQueries, EmailQueries>();
            builder.Services.AddScoped<IPasswordQueries, PasswordQueries>();
            builder.Services.AddScoped<IRoleQueries, RoleQueries>();
            builder.Services.AddScoped<IEmailCommands, EmailCommands>();
            builder.Services.AddScoped<IPasswordCommands, PasswordCommands>();
            builder.Services.AddScoped<IUserCommands, UserCommands>();
            builder.Services.AddScoped<IRoleCommands, RoleCommands>();
            builder.Services.AddScoped<ISessionQueries, SessionQueries>();
            builder.Services.AddScoped<ISessionCommands, SessionCommands>();
            builder.Services.AddScoped<IMagicLinkCommands, MagicLinkCommands>();
            builder.Services.AddScoped<IMagicLinkQueries, MagicLinkQueries>();
            builder.Services.AddScoped<IAuditActorProvider, HttpAuditActorProvider>();
            builder.Services.TryAddSingleton(TimeProvider.System);

            return builder;
        }

        WebApplicationBuilder AddFrontend()
        {
            builder.Services.AddStaticFrontend(
                builder.Configuration
                    .GetSection(FrontendOptions.SectionName)
                    .Get<FrontendOptions>() ?? FrontendOptions.Default);

            return builder;
        }

        public WebApplicationBuilder AddCore()
        {
            builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
            builder.Services.AddHttpContextAccessor();
                        
            return builder
                .AddTypeDescriptors()
                .AddOpenApi()
                .AddDatabase()
                .AddEmail()
                .AddServices()
                .AddSecurity()
                .AddFrontend();
        }
    }

    extension(WebApplication application)
    {
        public WebApplication UseCore()
        {
            application.UseCorrelationId();
            application.UseExceptionHandler(_ => { });
            application.UseStaticFrontend(
                EmbeddedFrontend.CreateFileProvider().WithWebRoot(application.Environment.WebRootPath));
            application.UseETag();
            application.MapOpenApi();

            application.MapGet("/config", ConfigEndpoint.ExecuteGet);
            application.MapGet("/auditLog", AuditLogEntryEndpoints.Get)
                .Produces<IEnumerable<AuditLogEntry>>()
                .Produces(204)
                .Produces(400)
                .Produces(500);

            application.UseRateLimiter();
            application.UseAuthentication();
            application.UseSessionValidation();
            application.UseAuthorization();
            application.UseDeletedRecordsVisibility();

            application.MapGet("/session", SessionEndpoints.Get)
                .Produces<SessionResponse>()
                .Produces(401)
                .Produces(409)
                .Produces(500);

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
                .Produces(401)
                .Produces(500);

            application.MapPost("/sign/in/magiclink", SignInEndpoints.RequestMagicLink)
                .AddEndpointFilter<DataAnnotationValidationFilter<SignInEndpoints.MagicLinkRequest>>()
                .RequireRateLimiting(MagicLinkExtensions.RequestRateLimitPolicy)
                .Produces(204)
                .Produces(400)
                .Produces(404)
                .Produces(429)
                .Produces(500);

            application.MapPost("/sign/in/magiclink/redeem", SignInEndpoints.SignInMagicLink)
                .AddEndpointFilter<DataAnnotationValidationFilter<SignInEndpoints.MagicLinkSignInRequest>>()
                .RequireRateLimiting(MagicLinkExtensions.RedeemRateLimitPolicy)
                .Produces(200)
                .Produces(400)
                .Produces(401)
                .Produces(404)
                .Produces(429)
                .Produces(500);

            application.MapPost("/sign/out", SignOutEndpoints.SignOut)
                .Produces(204)
                .Produces(401)
                .Produces(500);

            application.MapPost("/sign/out/all", SignOutEndpoints.SignOutAll)
                .Produces(204)
                .Produces(401)
                .Produces(500);

            return application;
        }
    }
}