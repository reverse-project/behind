using Behind.Common;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public static class MessageDraftExtensions
{
    extension(MessageEndpoints.SaveMessageRequest request)
    {
        public Option<IMessageCommands.MessageDraft> ToDraft()
        {
            var name = (request.Name ?? string.Empty).Trim();
            var title = (request.Title ?? string.Empty).Trim();
            var content = (request.Content ?? string.Empty).SanitizeRichText();

            return request.Level.ToMessageLevel()
                .Where(_ => name.Length is > 0 and <= Message.MaxNameLength)
                .Where(_ => title.Length <= Message.MaxTitleLength)
                .Where(_ => content.Length <= Message.MaxContentLength)
                .Where(_ => request.StartsAt.IsValidMessageSchedule(request.EndsAt))
                .Map(level => new IMessageCommands.MessageDraft(
                    name,
                    title,
                    content,
                    level,
                    request.StartsAt,
                    request.EndsAt));
        }
    }
}
