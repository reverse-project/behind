using Microsoft.AspNetCore.Http;

namespace Behind.Common.WebApi;

public static class HttpContextExtensions
{
    public const string UnknownClient = "unknown";

    extension(HttpContext context)
    {
        public string ClientAddress(int maxLength) =>
            (context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient).Truncate(maxLength);

        public string ClientUserAgent(int maxLength) =>
            (context.Request.Headers.UserAgent.FirstOrDefault() ?? UnknownClient).Truncate(maxLength);
    }

    extension(string value)
    {
        private string Truncate(int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
    }
}
