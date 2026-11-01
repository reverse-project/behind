using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Behind.Common.Configuration;
using Behind.Common.Converters;
using Behind.Common.Logging;
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
using Behind.Core.WebApi.Authorization;
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
using Microsoft.Extensions.Logging;
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
            OptionTypeConverterRegistration.Register<ElevationSettings>();
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

            builder.Services.AddElevationRateLimiting();

            builder.Services.AddMagicLink(
                securityConfiguration,
                new FileInfo(Path.Combine(builder.Environment.ContentRootPath, "security.json"))
            );

            builder.Configuration.AddJsonFile("frontend.json", true, true);
            builder.Services.Configure<FrontendConfiguration>(
                builder.Configuration.GetSection(FrontendConfiguration.SectionName)
            );
            builder.Services.Configure<SitesConfiguration>(
                builder.Configuration.GetSection(SitesConfiguration.SectionName)
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

            builder.Services.AddGrantAuthorization();

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
            builder.Services.AddScoped<IAccessQueries, AccessQueries>();
            builder.Services.AddScoped<IAccessCommands, AccessCommands>();
            builder.Services.AddScoped<ISessionQueries, SessionQueries>();
            builder.Services.AddScoped<ISessionCommands, SessionCommands>();
            builder.Services.AddScoped<IUserManagementQueries, UserManagementQueries>();
            builder.Services.AddScoped<IWallOfShameQueries, WallOfShameQueries>();
            builder.Services.AddScoped<IWallOfShameCommands, WallOfShameCommands>();
            builder.Services.AddScoped<ISiteQueries, SiteQueries>();
            builder.Services.AddScoped<ISiteCommands, SiteCommands>();
            builder.Services.AddScoped<ISiteImageQueries, SiteImageQueries>();
            builder.Services.AddScoped<ISiteImageCommands, SiteImageCommands>();
            builder.Services.AddScoped<UserManagementGate>();
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
            application.UseRequestLogging();
            application.UseExceptionHandler(_ => { });
            application.UseStaticFrontend(
                EmbeddedFrontend.CreateFileProvider().WithWebRoot(application.Environment.WebRootPath));
            application.UseETag();
            application.MapOpenApi();

            application.MapGet("/config", ConfigEndpoint.ExecuteGet)
                .RequireAuthorization(GrantName.Of(GrantArea.Configuration, GrantAction.Read));
            application.MapGet("/auditLog", AuditLogEntryEndpoints.Get)
                .RequireAuthorization(GrantName.Of(GrantArea.AuditLog, GrantAction.Read))
                .Produces<PageResponse<AuditLogItem>>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
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

            application.MapGet("/sessions", SessionListEndpoints.Get)
                .Produces<SessionListResponse>()
                .Produces(401)
                .Produces(500);

            application.MapGet("/about", AboutEndpoints.Get)
                .Produces<AboutResponse>();

            application.MapGet("/wallOfShame", WallOfShameEndpoints.Get)
                .Produces<PageResponse<WallOfShameItem>>()
                .Produces(500);

            var userManagement = GrantName.Of(GrantArea.User, GrantAction.Manage);

            application.MapGet("/users", UserManagementEndpoints.Get)
                .RequireAuthorization(userManagement)
                .Produces<UserListResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapPost("/users/{id:guid}/elevate/{role}", UserManagementEndpoints.Elevate)
                .RequireAuthorization(userManagement)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/users/{id:guid}/deelevate/{role}", UserManagementEndpoints.Deelevate)
                .RequireAuthorization(userManagement)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/users/{id:guid}/sign/out", UserManagementEndpoints.SignOut)
                .RequireAuthorization(userManagement)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/users/{id:guid}/ban", UserManagementEndpoints.Ban)
                .RequireAuthorization(userManagement)
                .AddEndpointFilter<DataAnnotationValidationFilter<UserManagementEndpoints.BanRequest>>()
                .Produces(204)
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/users/{id:guid}/unban", UserManagementEndpoints.Unban)
                .RequireAuthorization(userManagement)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/users/{id:guid}/delete", UserManagementEndpoints.Delete)
                .RequireAuthorization(GrantName.Of(GrantArea.User, GrantAction.Delete))
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/users/{id:guid}/restore", UserManagementEndpoints.Restore)
                .RequireAuthorization(userManagement)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/users/{id:guid}/hard/delete", UserManagementEndpoints.HardDelete)
                .RequireAuthorization(userManagement)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(500);

            application.MapPost("/users/{id:guid}/impersonate", ImpersonationEndpoints.Start)
                .RequireAuthorization(GrantName.Of(GrantArea.Impersonation, GrantAction.Manage))
                .Produces<SignInEndpoints.SignInResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/impersonation/stop", ImpersonationEndpoints.Stop)
                .Produces(204)
                .Produces(401)
                .Produces(409)
                .Produces(500);

            application.MapGet("/users/{id:guid}/access", UserAccessEndpoints.Get)
                .RequireAuthorization(userManagement)
                .Produces<UserAccessResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPut("/users/{id:guid}/access", UserAccessEndpoints.Set)
                .RequireAuthorization(userManagement)
                .AddEndpointFilter<DataAnnotationValidationFilter<UserAccessEndpoints.UserAccessRequest>>()
                .Produces(204)
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            var roleManagement = GrantName.Of(GrantArea.Role, GrantAction.Manage);

            application.MapGet("/roles", RoleManagementEndpoints.Get)
                .RequireAuthorization(roleManagement)
                .Produces<RoleListResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapPost("/roles", RoleManagementEndpoints.Create)
                .RequireAuthorization(roleManagement)
                .AddEndpointFilter<DataAnnotationValidationFilter<RoleManagementEndpoints.CreateRoleRequest>>()
                .Produces(204)
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(409)
                .Produces(500);

            application.MapPut("/roles/{id:guid}/access", RoleManagementEndpoints.SetAccess)
                .RequireAuthorization(roleManagement)
                .AddEndpointFilter<DataAnnotationValidationFilter<RoleManagementEndpoints.AccessRequest>>()
                .Produces(204)
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/roles/{id:guid}/restore", RoleManagementEndpoints.Restore)
                .RequireAuthorization(RoleManagementEndpoints.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/roles/{id:guid}/hard/delete", RoleManagementEndpoints.HardDelete)
                .RequireAuthorization(RoleManagementEndpoints.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(409)
                .Produces(500);

            application.MapPost("/roles/{id:guid}/delete", RoleManagementEndpoints.Delete)
                .RequireAuthorization(GrantName.Of(GrantArea.Role, GrantAction.Delete))
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            var siteRead = GrantName.Of(GrantArea.Site, GrantAction.Read);
            var siteWrite = GrantName.Of(GrantArea.Site, GrantAction.Write);

            application.MapGet("/sites/index", SiteEndpoints.GetIndex)
                .Produces<PublicSiteResponse>()
                .Produces(404)
                .Produces(500);

            application.MapGet("/sites/slug/{slug}", SiteEndpoints.GetPublic)
                .Produces<PublicSiteResponse>()
                .Produces(404)
                .Produces(500);

            application.MapGet("/sites/images/{id:guid}", SiteImageEndpoints.Get)
                .Produces(200)
                .Produces(404)
                .Produces(500);

            application.MapPost("/sites/images", SiteImageEndpoints.Upload)
                .RequireAuthorization(siteWrite)
                .DisableAntiforgery()
                .Produces<SiteImageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<SiteImageLimitResponse>(413)
                .Produces(415)
                .Produces(500);

            application.MapGet("/sites/settings", SiteEndpoints.GetSettings)
                .RequireAuthorization(siteRead)
                .Produces<SiteSettingsResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapGet("/sites", SiteEndpoints.List)
                .RequireAuthorization(siteRead)
                .Produces<SiteListResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapPost("/sites", SiteEndpoints.Create)
                .RequireAuthorization(siteWrite)
                .AddEndpointFilter<DataAnnotationValidationFilter<SiteEndpoints.SaveSiteRequest>>()
                .Produces<SiteSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<SiteConflictResponse>(409)
                .Produces(500);

            application.MapGet("/sites/{id:guid}", SiteEndpoints.GetHistory)
                .RequireAuthorization(siteRead)
                .Produces<SiteHistoryResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapGet("/sites/{id:guid}/versions/{version:long}", SiteEndpoints.GetVersion)
                .RequireAuthorization(siteRead)
                .Produces<SiteVersionResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/sites/{id:guid}/versions", SiteEndpoints.SaveVersion)
                .RequireAuthorization(siteWrite)
                .AddEndpointFilter<DataAnnotationValidationFilter<SiteEndpoints.SaveSiteRequest>>()
                .Produces<SiteSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<SiteConflictResponse>(409)
                .Produces(500);

            application.MapPost("/sites/{id:guid}/delete", SiteEndpoints.Delete)
                .RequireAuthorization(GrantName.Of(GrantArea.Site, GrantAction.Delete))
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/sites/{id:guid}/restore", SiteEndpoints.Restore)
                .RequireAuthorization(SiteEndpoints.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<SiteConflictResponse>(409)
                .Produces(500);

            application.MapPost("/sites/{id:guid}/hard/delete", SiteEndpoints.HardDelete)
                .RequireAuthorization(SiteEndpoints.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/sites/{id:guid}/versions/{version:long}/activate", SiteEndpoints.Activate)
                .RequireAuthorization(siteWrite)
                .Produces<SiteSavedResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<SiteConflictResponse>(409)
                .Produces(500);

            application.MapPut("/profile/displayName", ProfileEndpoints.ChangeDisplayName)
                .AddEndpointFilter<DataAnnotationValidationFilter<ProfileEndpoints.DisplayNameRequest>>()
                .Produces(204)
                .Produces(400)
                .Produces(401)
                .Produces(500);

            application.MapPost("/session/elevate/administrator", ElevationEndpoints.ElevateAdministrator)
                .AddEndpointFilter<DataAnnotationValidationFilter<ElevationEndpoints.ElevationRequest>>()
                .RequireRateLimiting(ElevationExtensions.RateLimitPolicy)
                .Produces(204)
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(429)
                .Produces(500);

            application.MapPost("/session/elevate/developer", ElevationEndpoints.ElevateDeveloper)
                .AddEndpointFilter<DataAnnotationValidationFilter<ElevationEndpoints.ElevationRequest>>()
                .RequireRateLimiting(ElevationExtensions.RateLimitPolicy)
                .Produces(204)
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(429)
                .Produces(500);

            application.MapPost("/sign/up/password", SignUpEndpoints.SignUpPassword)
                .AddEndpointFilter<DataAnnotationValidationFilter<SignUpEndpoints.PasswordRequest>>()
                .Produces(204)
                .Produces(400)
                .Produces(409)
                .Produces(500);

            application.MapPost(SignUpEndpoints.ConfirmEmailRoute, SignUpEndpoints.ConfirmEmail)
                .AddEndpointFilter<DataAnnotationValidationFilter<SignUpEndpoints.ConfirmEmailRequest>>()
                .Produces(204)
                .Produces(400)
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

            application.Logger.LogInformation(
                "Core web api configured for environment {EnvironmentName} with content root {ContentRootPath}",
                () => [application.Environment.EnvironmentName, application.Environment.ContentRootPath]);

            return application;
        }
    }
}