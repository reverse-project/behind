using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Common.WebApi;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.WebApi.Authorization;

public sealed class VfsFileLocalePermission(AbstractCoreContext database, IAuthorizationService authorization) : IContentLocalePermission
{
    public string EntityType => nameof(VfsFile);

    public async Task<bool> CanEdit(HttpContext context, Guid entityId)
    {
        var file = await database.VfsFiles.OfKey(entityId).LatestOf(database.VfsFiles)
            .SingleOrDefaultAsync(context.RequestAborted);
        if (file is null || file.Type == UserProfiles.FileType || file.Type == VfsTypes.AccessSettings
            || file.Type == VfsTypes.Layouts)
        {
            return false;
        }
        var policies = database.LocalizedContentTypes.Where(type =>
            file.Type == type.Name || file.Type.EndsWith(type.Name + "s", StringComparison.Ordinal)
            || type.Name.EndsWith("y", StringComparison.Ordinal)
                && file.Type.EndsWith(type.Name[..^1] + "ies", StringComparison.Ordinal))
            .Select(type => ContentLocaleEndpoints.EditPolicy(database, type.Name)).Where(policy => policy is not null).Distinct();
        foreach (var policy in policies)
        {
            if (await authorization.HoldsGrantAsync(context.User, policy!))
            {
                return true;
            }
        }
        return false;
    }
}
