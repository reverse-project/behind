using Behind.Core.WebApi;
using Behind.Web;

await WebApplication
    .CreateSlimBuilder(args)
    .AddCore()
    .Build()
    .UseCore()
    .UseWeb()
    .RunAsync();