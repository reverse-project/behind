using Behind.Common.WebApi.Infrastructure.Localization;
using Behind.Core.WebApi;

namespace Behind.Tests.Helpers;

public static class TestLocales
{
    public static LocaleCatalog Embedded { get; } = new(EmbeddedFrontend.CreateFileProvider());
}
