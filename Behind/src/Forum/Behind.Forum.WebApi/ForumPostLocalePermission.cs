using Behind.Common.WebApi;
using Behind.Forum.Common;
using Behind.Forum.Database;
using Behind.Forum.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Forum.WebApi;

public sealed class ForumPostLocalePermission(
    IModuleIdentity identity, ForumQueries queries, IAuthorizationService authorization) : IContentLocalePermission
{
    public string EntityType => nameof(ForumPost);

    public async Task<bool> CanEdit(HttpContext context, Guid entityId)
    {
        var user = await identity.UserId(context);
        if (user is null || !(await authorization.AuthorizeAsync(context.User, null, ForumNames.Read)).Succeeded)
        {
            return false;
        }
        var post = await queries.Post(entityId, context.RequestAborted);
        return post is not null && (post.Entity.AuthorId == user
            || (await authorization.AuthorizeAsync(context.User, null, ForumNames.Moderate)).Succeeded);
    }
}
