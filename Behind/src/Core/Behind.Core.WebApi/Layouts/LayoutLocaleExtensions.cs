using Behind.Core.Database;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Layouts;

public static class LayoutLocaleExtensions
{
    extension(SiteLayout layout)
    {
        public IReadOnlyList<Guid> ImageKeys =>
            layout.Badges.SelectMany(badge => badge.Image.ReferencedKeys(VfsTypes.LayoutImageUrl))
                .Concat(layout.Sections.SelectMany(section => section.Widgets)
                    .Where(widget => widget.Type == "core.custom")
                    .SelectMany(widget => widget.Content.ReferencedKeys(VfsTypes.LayoutImageUrl)))
                .Distinct().ToArray();
    }
}
