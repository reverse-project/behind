using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;

namespace Behind.Tests.Helpers;

public static class MenuTestBedExtensions
{
    public static IMenuItemCommands.MenuItemDraft ItemDraft(
        string title = "Docs",
        string url = "#/docs",
        MenuTarget target = MenuTarget.CurrentPage,
        MenuItemKind kind = MenuItemKind.Link,
        int position = 0,
        Option<Guid> parentId = default,
        IReadOnlyList<Guid>? roleIds = null) =>
        new(title, url, target, kind, position, parentId, roleIds ?? []);

    extension(NewsTestBed bed)
    {
        public async Task<TResult> WithMenuCommandsAsync<TResult>(Func<MenuCommands, Task<TResult>> action)
        {
            await using var ctx = bed.Write();

            return await action(new MenuCommands(ctx));
        }

        public async Task<TResult> WithMenuItemCommandsAsync<TResult>(Func<MenuItemCommands, Task<TResult>> action)
        {
            await using var ctx = bed.Write();

            return await action(new MenuItemCommands(ctx));
        }

        public async Task<Guid> CreateMenuAsync(string name = "footer", string title = "Footer")
        {
            var result = await bed.WithMenuCommandsAsync(commands => commands.Create(new IMenuCommands.MenuDraft(name, title)));

            return Assert.IsType<MenuSaveResult.Saved>(result).Id.Key;
        }

        public async Task<VersionedKey<Guid>> SaveMenuItemAsync(
            Guid? key,
            Guid menuId,
            IMenuItemCommands.MenuItemDraft draft)
        {
            var result = await bed.WithMenuItemCommandsAsync(
                commands => key is { } existing ? commands.Update(existing, draft) : commands.Create(menuId, draft));

            return Assert.IsType<MenuItemSaveResult.Saved>(result).Id;
        }

        public async Task<Guid> CreateMenuItemAsync(
            Guid menuId,
            string title = "Docs",
            string url = "#/docs",
            MenuItemKind kind = MenuItemKind.Link,
            int position = 0,
            Option<Guid> parentId = default,
            IReadOnlyList<Guid>? roleIds = null) =>
            (await bed.SaveMenuItemAsync(
                null,
                menuId,
                ItemDraft(title, url, MenuTarget.CurrentPage, kind, position, parentId, roleIds))).Key;

        public async Task<Guid> SeedRoleAsync(string name)
        {
            await using var ctx = bed.Read();

            return await DatabaseSeedHelper.SeedUserRoleAsync(ctx, name);
        }

        public async Task<Guid> SeedUserWithRolesAsync(string email, params Guid[] roleIds)
        {
            await using var ctx = bed.Read();
            var userId = await DatabaseSeedHelper.SeedHumanUserAsync(ctx, email);

            foreach (var roleId in roleIds)
            {
                await DatabaseSeedHelper.AssignRoleToUserAsync(ctx, userId, roleId);
            }

            return userId;
        }

        public async Task EnsureBuiltInMenusAsync()
        {
            await using var ctx = bed.Write();
            await ctx.EnsureBuiltInRolesAsync();
            await ctx.EnsureBuiltInMenusAsync();
        }

        public async Task<IReadOnlyList<IMenuQueries.MenuView>> ResolveMenusAsync(
            Option<Guid> viewerId,
            params string[] names)
        {
            await using var ctx = bed.Read();

            return await new MenuQueries(ctx).Resolve(names, viewerId);
        }
    }
}
