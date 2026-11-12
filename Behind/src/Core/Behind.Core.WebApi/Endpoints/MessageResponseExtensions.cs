using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public static class MessageResponseExtensions
{
    extension(IMessageQueries.MessageListing message)
    {
        public ManagedMessage ToItem(DeletionState state) =>
            new(
                message.Id.Key,
                message.Name,
                message.Title,
                message.Level.Key,
                message.StartsAt,
                message.EndsAt,
                state == DeletionState.Deleted);
    }

    extension(IMessageQueries.MessageDetails message)
    {
        public MessageDetailsResponse ToResponse(DeletionState state) =>
            new(
                message.Id.Key,
                message.Name,
                message.Title,
                message.Content,
                message.Level.Key,
                message.StartsAt,
                message.EndsAt,
                state == DeletionState.Deleted);

        public MessageWidgetResponse ToWidget() =>
            new(
                message.Id.Key,
                message.Id.Version,
                message.Title,
                message.Content.SanitizeRichText(),
                message.Level.Key,
                message.StartsAt,
                message.EndsAt);
    }
}
