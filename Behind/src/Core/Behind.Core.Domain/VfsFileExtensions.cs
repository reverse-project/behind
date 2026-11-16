using Behind.Common;

namespace Behind.Core.Domain;

public static class VfsFileExtensions
{
    extension(string? fileName)
    {
        public string ToVfsFileName(string fallback) =>
            fileName.ToSafeFileName(fallback, VfsFile.MaxFileNameLength);
    }

    extension(VfsFile file)
    {
        public string StorageKey => file.StorageKeyOf(file.Id.Key);

        public string StorageKeyOf(Guid key) =>
            file.ParentId is { } parentId
                ? $"{file.Type}/{parentId:D}/{key:D}"
                : $"{file.Type}/{key:D}";
    }
}
