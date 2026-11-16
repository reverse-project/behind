using Behind.Common.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Common.Database.Modules;

public static class ModuleDatabaseExtensions
{
    extension(ModelBuilder modelBuilder)
    {
        public ModelBuilder ApplyModuleConfigurations(IEnumerable<ModuleDatabase> databases)
        {
            foreach (var assembly in databases.Select(database => database.ConfigurationAssembly).Distinct())
            {
                modelBuilder.ApplyConfigurationsFromAssembly(assembly);
            }

            return modelBuilder;
        }
    }

    extension(ModuleDescriptor module)
    {
        public string MigrationsHistoryTable => $"__{module.Key}MigrationsHistory";
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddModuleContext<TInterface>() where TInterface : class =>
            services.AddScoped<TInterface>(provider =>
                provider.GetRequiredService<IUnitOfWork>() is TInterface context
                    ? context
                    : throw new InvalidOperationException(
                        $"The project database context does not implement {typeof(TInterface).Name}"));

        public IServiceCollection AddModuleMigrator<TContext>(string module) where TContext : DbContext =>
            services.AddScoped<IModuleMigrator>(provider =>
                new ContextModuleMigrator<TContext>(module, provider.GetRequiredService<TContext>()));
    }
}
