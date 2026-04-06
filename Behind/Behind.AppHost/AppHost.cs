var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("env");

var database = builder.AddPostgres("database")
    .WithImage("postgres", "18-alpine")
    .AddDatabase("behind");
var cache = builder.AddValkey("cache")
    .WithImage("valkey/valkey", "latest");

builder.AddProject<Projects.Behind_Web>("web", launchProfileName: null)
    .WithReference(database)
    .WithReference(cache)
    .WithHttpEndpoint(name: "http", port: 5001, targetPort: 5000, isProxied: true)
    .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:5000");

builder.Build().Run();