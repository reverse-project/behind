using Behind.Common.Database;
using Behind.Core.Database;
using Microsoft.Extensions.Logging;

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
    IUserApprovalQueries approvalQueries,
    IUserApprovalCommands approvalCommands,
    IRoleCommands roleCommands,
    ILogger<IdentityStore> logger
);