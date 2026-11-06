using Behind.Common;
using Behind.Common.Configuration;
using Behind.Common.Database;
using Behind.Core.Common.Configuration;
using Behind.Core.Database.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Behind.Core.Database;

public static class DatabaseServiceExtensions
{
    extension(IServiceProvider services)
    {
        private Option<string> ConnectionString(IConfiguration configuration) =>
            services.GetRequiredService<IOptions<DatabaseConfiguration>>().Value.Key
                .Bind(key => (Option<string>)configuration.GetConnectionString(key)!);
    }

    extension(DatabaseConfiguration database)
    {
        public IServiceCollection RegisterProvider<TProvider>(
            IServiceCollection services,
            IConfiguration configuration,
            Func<IServiceCollection, IServiceCollection> register)
            where TProvider : Provider
        {
            services.AddDatabaseConfiguration(configuration, database);

            return database.Provider.Match(
                none: () => services,
                some: provider => provider is TProvider ? register(services) : services);
        }
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddDatabaseConfiguration(IConfiguration configuration, DatabaseConfiguration fallback)
        {
            if (services.Any(descriptor => descriptor.ServiceType == typeof(ReloadableOption<DatabaseConfiguration>)))
            {
                return services;
            }

            return services.AddReloadableOptions(
                configuration,
                source => source.GetSection(DatabaseConfiguration.SectionName) is { } section && section.Exists()
                    ? section.GetOptionAware<DatabaseConfiguration>() ?? fallback
                    : fallback);
        }

        public IServiceCollection AddAuditedDatabase<TCore, TVersioning, TMigrator>(
            IConfiguration configuration,
            DatabaseProviderOptions provider)
            where TCore : AbstractCoreContext
            where TVersioning : AbstractVersioningContext
            where TMigrator : class, ICoreDatabaseMigrator
        {
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddScoped<IAuditActorProvider, AutonomousAuditActorProvider>();
            services.TryAddScoped<Func<IAuditActorProvider>>(provider =>
                provider.GetRequiredService<IAuditActorProvider>);
            services.TryAddScoped<AuditSaveChangesInterceptor>();

            return services.AddDatabase<TCore, TVersioning, TMigrator>(
                configuration,
                provider,
                (options, serviceProvider) =>
                    options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));
        }

        public IServiceCollection AddMigrationDatabase<TCore, TVersioning, TMigrator>(
            IConfiguration configuration,
            DatabaseProviderOptions provider)
            where TCore : AbstractCoreContext
            where TVersioning : AbstractVersioningContext
            where TMigrator : class, ICoreDatabaseMigrator =>
            services.AddDatabase<TCore, TVersioning, TMigrator>(configuration, provider, (_, _) => { });

        private IServiceCollection AddDatabase<TCore, TVersioning, TMigrator>(
            IConfiguration configuration,
            DatabaseProviderOptions provider,
            Action<DbContextOptionsBuilder, IServiceProvider> extend)
            where TCore : AbstractCoreContext
            where TVersioning : AbstractVersioningContext
            where TMigrator : class, ICoreDatabaseMigrator
        {
            services.AddDbContext<TCore>((serviceProvider, options) =>
            {
                extend(options, serviceProvider);
                serviceProvider.ConnectionString(configuration).Match(
                    none: () => { },
                    some: connectionString => provider.UseCore(options, connectionString));
            });
            services.AddDbContext<TVersioning>((serviceProvider, options) =>
            {
                extend(options, serviceProvider);
                serviceProvider.ConnectionString(configuration).Match(
                    none: () => { },
                    some: connectionString => provider.UseVersioning(options, connectionString));
            });
            services.AddScoped<AbstractVersioningContext>(serviceProvider =>
                serviceProvider.GetRequiredService<TVersioning>());
            services.AddScoped<AbstractCoreContext>(serviceProvider => serviceProvider.GetRequiredService<TCore>());
            services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<TCore>());
            services.AddScoped<ICoreDatabaseMigrator, TMigrator>();

            return services;
        }
    }
}
