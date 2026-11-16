namespace Behind.Common;

public static class UserLabelExtensions
{
    extension(Guid id)
    {
        public string ToUserLabel(string? displayName)
        {
            var name = displayName?.Trim();
            var hash = 14695981039346656037ul;
            foreach (var character in id.ToString("N"))
            {
                hash = unchecked((hash ^ character) * 1099511628211ul);
            }

            const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
            Span<char> tag = stackalloc char[8];
            for (var index = tag.Length - 1; index >= 0; index--)
            {
                tag[index] = alphabet[(int)(hash % 36)];
                hash /= 36;
            }

            return $"{(string.IsNullOrEmpty(name) ? "user" : name)}#{new string(tag)}";
        }
    }
}
