using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Identity;

public sealed class CaseSensitiveRoleManager(
    IRoleStore<ApplicationRole> store,
    IEnumerable<IRoleValidator<ApplicationRole>> validators,
    ILookupNormalizer normalizer,
    IdentityErrorDescriber errors,
    ILogger<RoleManager<ApplicationRole>> logger)
    : RoleManager<ApplicationRole>(store, validators, normalizer, errors, logger)
{
    public override string? NormalizeKey(string? key) => key;
}
