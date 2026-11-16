using Microsoft.AspNetCore.Http;

namespace Behind.Common.WebApi;

public interface IContentLocalePermission
{
    string EntityType { get; }
    Task<bool> CanEdit(HttpContext context, Guid entityId);
}
