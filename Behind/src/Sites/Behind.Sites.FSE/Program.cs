using Behind.Sites.FSE;

await WebApplication
    .CreateSlimBuilder(args)
    .AddFse()
    .Build()
    .UseFse()
    .RunAsync();
