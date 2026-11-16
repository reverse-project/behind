using Behind.Common.Modules;

namespace Behind.Core.WebApi;

public static class CoreModule
{
    public static ModuleDescriptor Descriptor { get; } = new("core", "Core")
    {
        Widgets = [new("core.locale-switch", "widgets.localeSwitch", "modules/core/models/widgets/locale-switch")]
    };
}
