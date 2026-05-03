namespace Behind.AppHost;

public static class AppHostExtensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        public IDistributedApplicationBuilder AddWeb()
        {
            builder
                .AddDockerCompose()
                .AddProject<Projects.Behind_Web>("web")
                //.WithReference(builder.AddDatabase())
                .WithReference(builder.AddCache());

            return builder;
        }
        IDistributedApplicationBuilder AddDockerCompose()
        {
            builder.AddDockerComposeEnvironment("env");

            return builder;
        }

        IResourceBuilder<ValkeyResource> AddCache() =>
            builder
                .AddValkey("cache")
                .WithImage("valkey/valkey", "latest");

        IResourceBuilder<PostgresDatabaseResource> AddDatabase() =>
            builder
                .AddPostgres("database")
                .WithImage("postgres", "18-alpine")
                .AddDatabase("behind");
    }
}