using Behind.Sites.RPDotHu.Database.Migrator;
using Microsoft.Extensions.Hosting;

await Host.CreateApplicationBuilder()
    .AddRpDotHuMigrator()
    .Build()
    .RunAsync();
