using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class AccessResponseExtensions
{
    public const string UnknownRoleReason = "unknown-role";

    extension(AccessSettings settings)
    {
        public AccessSettingsResponse ToResponse(bool isDeleted) =>
            new(
                settings.RestrictionEnabled,
                settings.RestrictedToRole,
                settings.RestrictionMessage,
                settings.ModeratedSignUp,
                isDeleted,
                settings.RestrictionTitle);
    }

    extension(AccessDecision decision)
    {
        public RestrictionStatusResponse ToResponse() =>
            decision switch
            {
                AccessDecision.Restricted restricted =>
                    new RestrictionStatusResponse(true, restricted.Message, restricted.Title),
                _ => new RestrictionStatusResponse(false, string.Empty)
            };
    }

    extension(IUserApprovalQueries.PendingUser user)
    {
        public PendingUserResponse ToResponse() => new(user.Id, user.DisplayName, user.Emails);
    }

    extension(AccessEndpoints.SaveAccessSettingsRequest request)
    {
        public IAccessSettingsCommands.AccessSettingsDraft ToDraft() =>
            new(
                request.RestrictionEnabled,
                request.RestrictedToRole.Trim(),
                (request.RestrictionMessage ?? string.Empty).SanitizeRichText().Trim(),
                request.ModeratedSignUp,
                (request.RestrictionTitle ?? string.Empty).Trim());
    }

    extension(AccessSettingsSaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                AccessSettingsSaveResult.Saved saved => Results.Json(new NewsSavedResponse(saved.Id.Key, saved.Id.Version)),
                AccessSettingsSaveResult.NotFound => Results.NotFound(),
                AccessSettingsSaveResult.UnknownRole => Conflict(UnknownRoleReason),
                _ => Conflict(NewsResponseExtensions.VersionConflictReason)
            };
    }

    private static IResult Conflict(string reason) => Results.Conflict(new NewsConflictResponse(reason));
}
