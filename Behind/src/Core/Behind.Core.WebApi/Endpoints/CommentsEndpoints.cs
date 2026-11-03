using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed record CommentsSettingsResponse(bool News);

public static class CommentsEndpoints
{
    public static IResult GetSettings(IOptionsSnapshot<CommentsConfiguration> configuration) =>
        Results.Json(new CommentsSettingsResponse(configuration.Value.IsEnabled(CommentsConfiguration.NewsEntity)));
}
