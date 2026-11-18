using Behind.Common.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests;

[Trait("Category", "Integration")]
public sealed class CommonSecurityGraphPostgresIntegrationTests
{
    [Fact]
    public async Task TraversalHandlesDeepCyclesAndRetainsDeletedOwnerMediaRestrictions()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image is required.")).Build();
        await postgres.StartAsync();
        await using var context = new GraphContext(new DbContextOptionsBuilder<GraphContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        await context.Database.EnsureCreatedAsync();
        var root = Guid.NewGuid();
        var portrait = Guid.NewGuid();
        var child = Guid.NewGuid();
        context.Nodes.AddRange(new GraphNode(root, 1, child, null, false),
            new GraphNode(child, 1, root, null, false),
            new GraphNode(Guid.NewGuid(), 1, root, portrait, true));
        var parent = child;
        for (var index = 0; index < 100; index++)
        {
            var key = Guid.NewGuid();
            context.Nodes.Add(new GraphNode(key, 1, parent, null, false));
            parent = key;
        }
        await context.SaveChangesAsync();

        var blocked = await context.InheritContentRestrictions([root]);

        Assert.Contains(parent, blocked);
        Assert.Contains(portrait, blocked);
        Assert.Equal(104, blocked.Length);
        var locale = await context.ResolveContentLocales([new ContentLocaleItem(nameof(GraphNode), root, "hu-HU")], "en-US");
        Assert.Contains(nameof(GraphNode) + ":" + parent, locale.HiddenKeys);
        Assert.Contains(nameof(GraphNode) + ":" + child, locale.HiddenKeys);
    }

    private sealed record GraphNode(Guid Key, long Version, Guid? ParentId, Guid? PictureId, bool Deleted);

    private sealed class GraphContext(DbContextOptions<GraphContext> options) : DbContext(options), IContentRestrictionContext, IContentLocaleContext
    {
        public DbSet<GraphNode> Nodes => Set<GraphNode>();
        public Guid[] HiddenContentIds { get; set; } = [];
        public bool IncludeAllContentLocales { get; set; }
        public string[] HiddenLocaleContentKeys { get; set; } = [];

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GraphNode>().HasKey(node => new { node.Key, node.Version });
            modelBuilder.Entity<GraphNode>().HasQueryFilter(SoftDeleteExtensions.FilterName, node => !node.Deleted);
            modelBuilder.ApplyContentRestrictions(this, [typeof(GraphNode)]);
            modelBuilder.ApplyContentLocales(this, [typeof(GraphNode)]);
        }
    }
}
