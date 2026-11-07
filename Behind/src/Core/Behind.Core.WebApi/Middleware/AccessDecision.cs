namespace Behind.Core.WebApi.Middleware;

public abstract record AccessDecision
{
    public sealed record Open : AccessDecision;

    public sealed record Restricted(string Message, string Title = "") : AccessDecision;
}
