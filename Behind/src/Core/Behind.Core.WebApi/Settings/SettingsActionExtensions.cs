using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Settings;

public static class SettingsActionExtensions
{
    extension(string file)
    {
        public Option<SettingsAction> FindAction(string path) =>
            string.Equals(file, SettingsAction.SecurityFile, StringComparison.OrdinalIgnoreCase)
                ? SettingsAction.All.Find(action => string.Equals(action.Path, path, StringComparison.OrdinalIgnoreCase))
                : None;

        public Option<SettingsAction> FindAction(string path, SettingsActionKind kind) =>
            file.FindAction(path).Bind(action => action.Kind == kind ? Some(action) : None);
    }
}
