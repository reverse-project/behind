namespace Behind.Core.WebApi.Endpoints;

public sealed record AboutResponse(string Name, string Version)
{
    public const string ProductName = "Behind";

    public static AboutResponse Current { get; } = new(ProductName, typeof(ICoreWebApi).Assembly.ProductVersion);
}
