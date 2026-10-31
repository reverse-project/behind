using Behind.Common.Database;
using Behind.Core.Database;

namespace Behind.Core.WebApi.Identity;

public partial class IdentityStore(
    IUnitOfWork unitOfWork,
    IUserQueries userQueries,
    IEmailQueries emailQueries,
    IEmailCommands emailCommands,
    IPasswordQueries passwordQueries,
    IRoleQueries roleQueries,
    IPasswordCommands passwordCommands,
    IUserCommands userCommands,
    IRoleCommands roleCommands
);