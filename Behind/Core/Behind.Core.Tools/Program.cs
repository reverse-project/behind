using Behind.Core.Tools;
using Microsoft.Extensions.Hosting;

await Host
    .CreateApplicationBuilder()
    .AddTools()
    .Build()
    .RunAsync();