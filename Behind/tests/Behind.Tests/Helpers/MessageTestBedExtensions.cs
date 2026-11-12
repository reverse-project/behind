using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Domain;

namespace Behind.Tests.Helpers;

public static class MessageTestBedExtensions
{
    public static IMessageCommands.MessageDraft MessageDraft(
        string name = "maintenance",
        string title = "Maintenance",
        string content = "<p>Down tonight</p>",
        MessageLevel level = MessageLevel.Warning,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null) =>
        new(name, title, content, level, startsAt, endsAt);

    extension(NewsTestBed bed)
    {
        public async Task<TResult> WithMessageCommandsAsync<TResult>(Func<MessageCommands, Task<TResult>> action)
        {
            await using var ctx = bed.Write();

            return await action(new MessageCommands(ctx));
        }

        public async Task<TResult> WithMessageQueriesAsync<TResult>(Func<MessageQueries, Task<TResult>> action)
        {
            await using var ctx = bed.Read();

            return await action(new MessageQueries(ctx));
        }

        public async Task<Guid> CreateMessageAsync(IMessageCommands.MessageDraft? draft = null)
        {
            var result = await bed.WithMessageCommandsAsync(commands => commands.Create(draft ?? MessageDraft()));

            return Assert.IsType<VersionedSaveResult.Saved>(result).Id.Key;
        }
    }
}
