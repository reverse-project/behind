using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Authorization;

public sealed class NewsCommentLocalePermission(
    IModuleIdentity identity, INewsCommentQueries comments, IAuthorizationService authorization) : IContentLocalePermission
{
    public string EntityType => nameof(NewsComment);

    public async Task<bool> CanEdit(HttpContext context, Guid entityId)
    {
        var user = await identity.UserId(context);
        if (user is null)
        {
            return false;
        }
        var author = await comments.FindAuthor(entityId, context.RequestAborted);
        return await author.Match(
            () => Task.FromResult(false),
            async id => id == user || await authorization.HoldsGrantAsync(context.User, NewsCommentEndpoints.EditGrant));
    }
}
