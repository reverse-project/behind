using Behind.Core.Common;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Identity;

public sealed class ApplicationUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public IEnumerable<Email> Emails { get; set; } = Enumerable.Empty<Email>();
    public PasswordHash? Password { get; set; }
}