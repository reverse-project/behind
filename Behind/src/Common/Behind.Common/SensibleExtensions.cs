using System.Reflection;

namespace Behind.Common;

public static class SensibleExtensions
{
    public const string MaskedValue = "***";

    extension(MemberInfo member)
    {
        public bool IsSensible => member.GetCustomAttribute<SensibleAttribute>() is not null;
    }
}
