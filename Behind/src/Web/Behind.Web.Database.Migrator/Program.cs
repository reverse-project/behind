using Behind.Web.Database.Migrator;
using Microsoft.Extensions.Hosting;

await Host.CreateApplicationBuilder()
    .AddWebMigrator()
    .Build()
    .RunAsync();
