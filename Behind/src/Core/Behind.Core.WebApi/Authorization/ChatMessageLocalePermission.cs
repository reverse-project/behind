using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Authorization;

public sealed class ChatMessageLocalePermission(
    IModuleIdentity identity, IChatStore messages, IAuthorizationService authorization, TimeProvider time) : IContentLocalePermission
{
    public string EntityType => nameof(ChatMessage);

    public async Task<bool> CanEdit(HttpContext context, Guid entityId)
    {
        var user = await identity.UserId(context);
        if (user is null || !await authorization.HoldsGrantAsync(context.User, ChatEndpoints.ReadGrant)
            || !await authorization.HoldsGrantAsync(context.User, ChatEndpoints.WriteGrant))
        {
            return false;
        }
        var message = (await messages.Find([entityId], context.RequestAborted)).SingleOrDefault();
        return message is not null && message.AuthorId == user
            && time.GetUtcNow() < message.CreatedAt + ChatMessage.EditWindow;
    }
}
