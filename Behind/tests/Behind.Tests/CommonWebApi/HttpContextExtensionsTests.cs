using System.Net;
using Behind.Common.WebApi;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CommonWebApi;

public class HttpContextExtensionsTests
{
    [Fact]
    public void ClientAddress_WithRemoteIp_ReturnsIp()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.5");

        Assert.Equal("192.168.1.5", context.ClientAddress(255));
    }

    [Fact]
    public void ClientAddress_WithoutRemoteIp_ReturnsUnknown()
    {
        Assert.Equal(HttpContextExtensions.UnknownClient, new DefaultHttpContext().ClientAddress(255));
    }

    [Fact]
    public void ClientUserAgent_WithHeader_ReturnsHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = "Mozilla";

        Assert.Equal("Mozilla", context.ClientUserAgent(1000));
    }

    [Fact]
    public void ClientUserAgent_WithoutHeader_ReturnsUnknown()
    {
        Assert.Equal(HttpContextExtensions.UnknownClient, new DefaultHttpContext().ClientUserAgent(1000));
    }

    [Fact]
    public void ClientUserAgent_LongerThanMax_IsTruncated()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = new string('a', 50);

        Assert.Equal(new string('a', 10), context.ClientUserAgent(10));
    }
}
