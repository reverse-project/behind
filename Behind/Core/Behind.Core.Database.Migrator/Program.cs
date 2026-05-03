using Behind.Core.Database.Migrator;
using Behind.Core.Database.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder();
builder.Services.AddHostedService<MigratorService>();
builder.Services.AddDbContext<CoreContextSqlite>(options => options.UseSqlite("Data Source=/workspace/Data/core.db"));
var app = builder.Build();
await app.RunAsync();