namespace Behind.Core.Domain;

public static class RouteTargetExtensions
{
    private const string RoutePrefix = "#/";

    extension(string? target)
    {
        public bool IsValidRouteTarget =>
            !string.IsNullOrEmpty(target)
            && target.Length <= RouteAlias.MaxTargetLength
            && target.Length > RoutePrefix.Length
            && target.StartsWith(RoutePrefix, StringComparison.Ordinal)
            && !target.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
            && !target.Contains('\\');
    }
}
