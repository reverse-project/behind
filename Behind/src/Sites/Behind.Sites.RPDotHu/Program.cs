using Behind.Sites.RPDotHu;

await WebApplication
    .CreateSlimBuilder(args)
    .AddRpDotHu()
    .Build()
    .UseRpDotHu()
    .RunAsync();
