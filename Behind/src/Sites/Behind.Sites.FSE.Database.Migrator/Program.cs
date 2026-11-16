using Behind.Sites.FSE.Database.Migrator;
using Microsoft.Extensions.Hosting;

await Host.CreateApplicationBuilder()
    .AddFseMigrator()
    .Build()
    .RunAsync();
