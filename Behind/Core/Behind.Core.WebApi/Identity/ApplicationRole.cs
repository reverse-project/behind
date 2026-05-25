namespace Behind.Core.WebApi.Identity;

public sealed class ApplicationRole
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
}