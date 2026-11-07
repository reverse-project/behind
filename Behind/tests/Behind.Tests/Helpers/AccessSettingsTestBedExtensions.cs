using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;

namespace Behind.Tests.Helpers;

public static class AccessSettingsTestBedExtensions
{
    public static IAccessSettingsCommands.AccessSettingsDraft AccessDraft(
        bool restrictionEnabled = true,
        string role = "Users",
        string message = "Members only",
        bool moderatedSignUp = false) =>
        new(restrictionEnabled, role, message, moderatedSignUp);

    extension(NewsTestBed bed)
    {
        public async Task EnsureRolesAsync()
        {
            await using var ctx = bed.Write();
            await ctx.EnsureBuiltInRolesAsync(AccessCatalog.Core);
        }

        public async Task<TResult> WithAccessSettingsCommandsAsync<TResult>(
            Func<AccessSettingsCommands, Task<TResult>> action)
        {
            await using var ctx = bed.Write();

            return await action(new AccessSettingsCommands(ctx, new VfsCommands(ctx, bed.ContentStore)));
        }

        public async Task<TResult> WithAccessSettingsQueriesAsync<TResult>(
            Func<AccessSettingsQueries, Task<TResult>> action)
        {
            await using var ctx = bed.Read();

            return await action(new AccessSettingsQueries(ctx));
        }

        public async Task WithUserApprovalCommandsAsync(Func<UserApprovalCommands, Task> action)
        {
            await using var ctx = bed.Write();

            await action(new UserApprovalCommands(ctx));
        }

        public async Task<TResult> WithUserApprovalCommandsAsync<TResult>(
            Func<UserApprovalCommands, Task<TResult>> action)
        {
            await using var ctx = bed.Write();

            return await action(new UserApprovalCommands(ctx));
        }

        public async Task<TResult> WithUserApprovalQueriesAsync<TResult>(
            Func<UserApprovalQueries, Task<TResult>> action)
        {
            await using var ctx = bed.Read();

            return await action(new UserApprovalQueries(ctx));
        }

        public async Task<AccessSettingsSaveResult.Saved> SaveAccessSettingsAsync(
            IAccessSettingsCommands.AccessSettingsDraft? draft = null)
        {
            await bed.EnsureRolesAsync();

            var result = await bed.WithAccessSettingsCommandsAsync(commands => commands.Save(draft ?? AccessDraft()));

            return Assert.IsType<AccessSettingsSaveResult.Saved>(result);
        }
    }
}
