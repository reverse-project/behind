using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Settings;

public static class SettingsActionExtensions
{
    extension(string file)
    {
        public Option<SettingsAction> FindAction(string path) =>
            file == SettingsAction.SecurityFile
                ? SettingsAction.All.Find(action => action.Path == path)
                : None;

        public Option<SettingsAction> FindAction(string path, SettingsActionKind kind) =>
            file.FindAction(path).Bind(action => action.Kind == kind ? Some(action) : None);
    }
}
