using Behind.AppHost;

DistributedApplication.CreateBuilder(args)
    .AddWeb()
    .Build()
    .Run();