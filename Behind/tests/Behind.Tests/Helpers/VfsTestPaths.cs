namespace Behind.Tests.Helpers;

public static class VfsTestPaths
{
    public static string Of(string type, Guid key, Guid? parentId = null) =>
        new VfsFile(type, string.Empty, string.Empty, 0, parentId).StorageKeyOf(key);
}
