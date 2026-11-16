namespace Behind.Common.WebApi.Middleware;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IgnoreETagAttribute : Attribute;
