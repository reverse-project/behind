using Behind.Common.Database;
using Behind.Core.Database;

namespace Behind.Core.WebApi.Identity;

public partial class IdentityStore(
    IUnitOfWork unitOfWork,
    IUserQueries userQueries,
    IEmailQueries emailQueries,
    IPasswordQueries passwordQueries,
    IPasswordCommands passwordCommands,
    IUserCommands userCommands
);