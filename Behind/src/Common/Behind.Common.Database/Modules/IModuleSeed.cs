namespace Behind.Common.Database.Modules;

public interface IModuleSeed
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
