using Behind.Common.Storage;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreCommon;

public class BackendConfigurationTests
{
    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void SectionName_IsBackend()
    {
        Assert.Equal("Backend", BackendConfiguration.SectionName);
    }

    [Fact]
    public void VfsRoot_IsTheVfsFolderOfTheDataDirectory()
    {
        var configuration = new BackendConfiguration { DataDirectory = "/srv/data" };

        Assert.Equal(Path.Combine("/srv/data", "vfs"), configuration.VfsRoot);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void VfsRoot_WithoutADataDirectory_Throws(string? directory)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => new BackendConfiguration { DataDirectory = directory }.VfsRoot);

        Assert.Contains("Backend:DataDirectory", exception.Message);
    }

    [Fact]
    public async Task AddVfsContentStore_RegistersAStoreRootedInTheConfiguredDataDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"behind-backend-{Guid.NewGuid():N}");

        try
        {
            using var provider = new ServiceCollection()
                .AddVfsContentStore(Configuration(new() { ["Backend:DataDirectory"] = directory }))
                .BuildServiceProvider();

            await provider.GetRequiredService<IContentStore>().Write("a/file", [1]);

            Assert.True(File.Exists(Path.Combine(directory, "vfs", "a", "file")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void AddVfsContentStore_WithoutADataDirectory_FailsAtRegistration()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddVfsContentStore(Configuration(new())));
    }
}
