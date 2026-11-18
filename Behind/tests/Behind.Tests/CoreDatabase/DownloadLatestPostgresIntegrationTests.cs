using System.Data.Common;
using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

[Trait("Category", "Integration")]
public sealed class DownloadLatestPostgresIntegrationTests
{
    [Fact]
    public async Task LatestPagesVisibleDownloadsByAuditCreationBeforeLoadingMetadata()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image is required.")).Build();
        await postgres.StartAsync();
        var recorder = new CommandRecorder();
        await using var context = new CoreContextPostgres(new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(postgres.GetConnectionString()).AddInterceptors(recorder).Options);
        await context.Database.EnsureCreatedAsync();
        var user = await PostgresSecuritySeed.User(context, "downloads@test.example");
        var folderId = Guid.NewGuid();
        var folder = new DownloadFolder(new VersionedKey<Guid>(folderId, 1), "folder", "Folder");
        context.DownloadFolders.Add(folder);
        context.Entry(folder).StampVersionedKey(folderId, 1);
        var keys = new List<Guid>();
        for (var index = 0; index < 3; index++)
        {
            var id = Guid.NewGuid();
            var fileId = Guid.NewGuid();
            keys.Add(id);
            var download = new Download(new VersionedKey<Guid>(id, 1), index == 2 ? "A newest" : "Z older");
            context.Downloads.Add(download);
            context.Entry(download).StampVersionedKey(id, 1);
            context.Entry(download).Property<Guid>(DownloadConfiguration.FolderIdProperty).CurrentValue = folderId;
            context.Entry(download).Property<Guid>(DownloadConfiguration.FileIdProperty).CurrentValue = fileId;
            context.Entry(download).Property<Guid>(DownloadConfiguration.UploadedByIdProperty).CurrentValue = user;
            var file = new VfsFile(VfsTypes.Downloads, "file.txt", "text/plain", 1, id);
            context.VfsFiles.Add(file);
            context.Entry(file).StampVersionedKey(fileId, 1);
            context.AuditLogEntries.Add(new AuditLogEntry(nameof(Download), $"{id}:1", new AuditLogEntry.Created(),
                DateTimeOffset.UtcNow.AddDays(index - 3)));
        }
        await context.SaveChangesAsync();
        recorder.Commands.Clear();

        var result = await new DownloadQueries(context, Substitute.For<IVfsQueries>()).Latest(1);

        Assert.Equal(keys[2], Assert.Single(result).Id);
        Assert.Contains(recorder.Commands, command => command.Contains("FROM \"Downloads\"", StringComparison.Ordinal)
            && command.Contains("LIMIT", StringComparison.Ordinal));
        Assert.Empty(await new DownloadQueries(context, Substitute.For<IVfsQueries>()).Latest(0));
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
