namespace Behind.Core.WebApi.Identity;

public sealed class ApplicationRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
}