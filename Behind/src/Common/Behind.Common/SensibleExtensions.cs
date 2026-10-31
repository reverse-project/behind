using System.Reflection;

namespace Behind.Common;

public static class SensibleExtensions
{
    extension(MemberInfo member)
    {
        public bool IsSensible => member.GetCustomAttribute<SensibleAttribute>() is not null;
    }
}
