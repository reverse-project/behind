using Behind.Core.Common;

namespace Behind.Core.WebApi.Identity;

public sealed class ApplicationUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public PasswordHash? Password { get; set; } 
}