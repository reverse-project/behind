using Behind.Web;

await WebApplication
    .CreateSlimBuilder(args)
    .AddWeb()
    .Build()
    .UseWeb()
    .RunAsync();