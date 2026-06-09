using Behind.Common.Database;
using Behind.Core.Database;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Identity;

public partial class IdentityStore(
    IUnitOfWork unitOfWork,
    IUserQueries userQueries,
    IEmailQueries emailQueries,
    IPasswordQueries passwordQueries,
    IRoleQueries roleQueries,
    IPasswordCommands passwordCommands,
    IUserCommands userCommands,
    IRoleCommands roleCommands,
    IHttpContextAccessor httpContextAccessor
);