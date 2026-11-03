using Behind.Core.Domain;

namespace Behind.Core.Database;

public static class VfsCommandExtensions
{
    extension(IVfsCommands files)
    {
        public Task AttachSiteImagesOf(
            string type,
            Guid parentId,
            IEnumerable<string?> contents,
            CancellationToken cancellationToken = default) =>
            files.Attach(
                type,
                parentId,
                contents
                    .SelectMany(content => VfsTypes.ReferencedImageUrlsOf(type).SelectMany(content.ReferencedKeys))
                    .Distinct()
                    .ToList(),
                cancellationToken);
    }
}
