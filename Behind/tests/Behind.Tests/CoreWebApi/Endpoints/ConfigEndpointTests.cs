using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OtpNet;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class ConfigEndpointTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 20).Select(i => (byte)i).ToArray();
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private static IOptions<SecurityConfiguration> Security()
    {
        Behind.Core.Common.Configuration.Totp.TryCreate(Base32Encoding.ToString(Key), out var totp);

        return Options.Create(new SecurityConfiguration(false, Some<SecretKey>(totp), None, None));
    }

    private static string CodeFor(DateTimeOffset at) =>
        new OtpNet.Totp(Key, mode: Behind.Core.Common.Configuration.Totp.HashMode).ComputeTotp(at.UtcDateTime);

    private static DateTimeOffset FirstMomentWith(Func<string, bool> predicate) =>
        Enumerable.Range(0, 10_000)
            .Select(step => Start.AddSeconds(step * 30))
            .First(moment => predicate(CodeFor(moment)));

    private static IResult Get(DateTimeOffset now, string code) =>
        ConfigEndpoint.ExecuteGet(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:SecretKey"] = "totp://AAAA",
                    ["Security:Jwt:SigningKey"] = "signing",
                    ["Security:MagicLink:ActualSecret"] = "actual",
                    ["Security:MagicLink:OldSecret"] = "old",
                    ["Security:Elevation:DeveloperSecretKey"] = "developer"
                })
                .Build(),
            Security(),
            new FixedTimeProvider(now),
            code);

    private static int StatusOf(IResult result) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode ?? StatusCodes.Status200OK;

    [Fact]
    public void ExecuteGet_CodeWithLeadingZero_IsAccepted()
    {
        var now = FirstMomentWith(code => code.StartsWith('0'));

        Assert.NotEqual(StatusCodes.Status403Forbidden, StatusOf(Get(now, CodeFor(now))));
    }

    [Fact]
    public void ExecuteGet_CodeWithoutItsLeadingZero_IsRejected()
    {
        var now = FirstMomentWith(code => code.StartsWith('0'));

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(Get(now, CodeFor(now).TrimStart('0'))));
    }

    [Fact]
    public void ExecuteGet_WrongCode_IsRejected()
    {
        var now = FirstMomentWith(code => !code.StartsWith('0'));

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(Get(now, CodeFor(now.AddMinutes(-10)))));
    }
}
