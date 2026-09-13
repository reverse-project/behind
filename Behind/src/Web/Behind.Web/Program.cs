using Behind.Core.WebApi;
using Behind.Web;

await WebApplication
    .CreateSlimBuilder(args)
    .AddWebApi()
    .Build()
    .UseCore()
    .UseWeb()
    .RunAsync();