using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Mcp;
using Behind.Common.WebApi.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Mcp;

public sealed class McpServerTests
{
    private static DefaultHttpContext Request(string json)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Headers.Accept = "application/json, text/event-stream";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return context;
    }

    private static McpServer Server(IMcpRequestDispatcher dispatcher, int maximumBytes = 1048576)
    {
        var options = Substitute.For<IOptionsMonitor<McpConfiguration>>();
        options.CurrentValue.Returns(new McpConfiguration { MaxRequestBytes = maximumBytes });
        var modules = Substitute.For<IModuleMcpToolCatalog>();
        modules.List().Returns(Array.Empty<ModuleMcpTool>());
        return new McpServer(dispatcher, modules, options, NullLogger<McpServer>.Instance);
    }

    private static JsonObject Body(IResult result) =>
        Assert.IsType<JsonHttpResult<JsonObject>>(result).Value!;

    [Fact]
    public async Task Initialize_NegotiatesVersionAndAdvertisesOnlyImplementedCapabilities()
    {
        var result = await Server(Substitute.For<IMcpRequestDispatcher>()).Handle(Request("""
            {"jsonrpc":"2.0","id":"init","method":"initialize","params":{
              "protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}
            """), default);
        var response = Body(result);
        Assert.Equal("init", response["id"]!.GetValue<string>());
        Assert.Equal("2025-06-18", response["result"]!["protocolVersion"]!.GetValue<string>());
        Assert.Single(response["result"]!["capabilities"]!.AsObject());
        Assert.NotNull(response["result"]!["capabilities"]!["tools"]);
        Assert.False(response.ContainsKey("error"));
    }

    [Theory]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"api_request","arguments":{"method":"GET","path":"/news","query":{"page":1}}}}""", -32602)]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"missing"}}""", -32602)]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"missing"}""", -32601)]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"ping","method":"tools/call"}""", -32600)]
    [InlineData("""{"jsonrpc":"2.0","method":"tools/call","params":{"name":"api_request","arguments":{"method":"POST","path":"/news"}}}""", -32600)]
    [InlineData("""[{"jsonrpc":"2.0","id":1,"method":"ping"}]""", -32600)]
    [InlineData("{", -32700)]
    public async Task InvalidMessages_NeverDispatchCommands(string json, int error)
    {
        var dispatcher = Substitute.For<IMcpRequestDispatcher>();
        var result = await Server(dispatcher).Handle(Request(json), default);
        Assert.Equal(error, Body(result)["error"]!["code"]!.GetValue<int>());
        await dispatcher.DidNotReceiveWithAnyArgs().Dispatch(default!, default, default);
    }

    [Fact]
    public async Task ToolCall_PreservesNumericIdsAndSurfacesForbiddenOperationsAsToolErrors()
    {
        var dispatcher = Substitute.For<IMcpRequestDispatcher>();
        dispatcher.Dispatch(default!, default, default).ReturnsForAnyArgs(call =>
        {
            Assert.Equal("/news", call.Arg<JsonElement>().GetProperty("path").GetString());
            return new McpApiResponse(403, "Forbidden");
        });
        var context = Request("""
            {"jsonrpc":"2.0","id":42,"method":"tools/call","params":{"name":"api_request",
              "arguments":{"method":"POST","path":"/news","body":{"title":"Example"}}}}
            """);
        var result = await Server(dispatcher).Handle(context, default);
        var response = Body(result);
        Assert.Equal(42, response["id"]!.GetValue<int>());
        Assert.True(response["result"]!["isError"]!.GetValue<bool>());
        using var text = JsonDocument.Parse(response["result"]!["content"]![0]!["text"]!.GetValue<string>());
        Assert.Equal(403, text.RootElement.GetProperty("status").GetInt32());
        await dispatcher.Received(1).Dispatch(context, Arg.Any<JsonElement>(), default);
    }

    [Fact]
    public async Task Notifications_AreAcceptedWithoutDispatching()
    {
        var dispatcher = Substitute.For<IMcpRequestDispatcher>();
        var result = await Server(dispatcher).Handle(
            Request("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""), default);
        Assert.Equal(202, Assert.IsType<Accepted>(result).StatusCode);
        await dispatcher.DidNotReceiveWithAnyArgs().Dispatch(default!, default, default);
    }

    [Fact]
    public async Task RequestLimits_ApplyToBodiesWithoutContentLength()
    {
        var dispatcher = Substitute.For<IMcpRequestDispatcher>();
        var result = await Server(dispatcher, 8).Handle(
            Request("""{"jsonrpc":"2.0","id":1,"method":"ping"}"""), default);
        Assert.Equal(413, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await dispatcher.DidNotReceiveWithAnyArgs().Dispatch(default!, default, default);
    }

    [Fact]
    public async Task UnsupportedTransportHeaders_AreRejected()
    {
        var context = Request("""{"jsonrpc":"2.0","id":1,"method":"ping"}""");
        context.Request.Headers.Accept = "application/json";
        Assert.Equal(406, Assert.IsType<StatusCodeHttpResult>(
            await Server(Substitute.For<IMcpRequestDispatcher>()).Handle(context, default)).StatusCode);
        context.Request.Headers.Accept = "application/json, text/event-stream";
        context.Request.Headers["MCP-Protocol-Version"] = "unsupported";
        Assert.Equal(400, Assert.IsType<JsonHttpResult<JsonObject>>(
            await Server(Substitute.For<IMcpRequestDispatcher>()).Handle(context, default)).StatusCode);
    }

    [Theory]
    [InlineData("/news", true)]
    [InlineData("/users", true)]
    [InlineData("/sport/teams", true)]
    [InlineData("https://example.com/news", false)]
    [InlineData("//example.com/news", false)]
    [InlineData("/news/../mcp", false)]
    [InlineData("/%6dcp", false)]
    [InlineData("/news?path=/mcp", false)]
    [InlineData("/MCP", false)]
    [InlineData("/service-users/123/rotate", false)]
    [InlineData("/session/elevate/developer", false)]
    [InlineData("/users/123/impersonate", false)]
    [InlineData("/users/123/impersonate/", false)]
    [InlineData("/users/123/impersonate///", false)]
    [InlineData("/users/123/sign/out/", false)]
    public void DispatchPaths_CannotEscapeTheLocalApplicationOrIssueInteractiveCredentials(string path, bool allowed) =>
        Assert.Equal(allowed, McpRequestDispatcher.IsAllowedPath(path));
}
