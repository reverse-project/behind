using System.Reflection;

namespace Behind.Core.WebApi.Endpoints;

public static class ProductVersionExtensions
{
    private const string FallbackVersion = "0.0.0";
    private const char BuildMetadataSeparator = '+';

    extension(Assembly assembly)
    {
        public string ProductVersion =>
            (assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
             ?? assembly.GetName().Version?.ToString()
             ?? FallbackVersion)
            .Split(BuildMetadataSeparator)[0];
    }
}
