using Behind.Core.Database.Migrator;
using Microsoft.Extensions.Hosting;

await Host.CreateApplicationBuilder()
    .AddMigrator()
    .Build()
    .RunAsync();