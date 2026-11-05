using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Behind.Core.WebApi.Captcha;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi.Captcha;

public sealed class CaptchaHttpTests : IAsyncLifetime
{
    private readonly byte[] _key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(CaptchaService.KeySize);
    private WebApplication _application = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ICaptchaService>(new CaptchaService(TimeProvider.System, _key));

        _application = builder.Build();
        _application.MapGet(CaptchaExtensions.ChallengeRoute, CaptchaEndpoints.GetChallenge);
        _application.MapPost("/sign/in/password", (SignInEndpoints.PasswordRequest request) => Results.Ok("signed in"))
            .RequireCaptcha<SignInEndpoints.PasswordRequest>();
        await _application.StartAsync();

        var address = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    private async Task<JsonNode> Challenge() =>
        (await _client.GetFromJsonAsync<JsonNode>(CaptchaExtensions.ChallengeRoute))!;

    private string AnswerOf(JsonNode challenge) =>
        CaptchaTicket.Open((string)challenge["id"]!, _key).Match(
            () => throw new InvalidOperationException(),
            ticket => ticket.Answer);

    private Task<HttpResponseMessage> SignIn(JsonNode? captcha) =>
        _client.PostAsJsonAsync(
            "/sign/in/password",
            new JsonObject { ["email"] = "a@b.c", ["password"] = "pw", ["captcha"] = captcha });

    [Fact]
    public async Task Challenge_IsServedAsAnUncachedPngDataUri()
    {
        var response = await _client.GetAsync(CaptchaExtensions.ChallengeRoute);
        var body = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.StartsWith("data:image/png;base64,", (string)body["image"]!);
        Assert.False(string.IsNullOrEmpty((string)body["id"]!));
    }

    [Fact]
    public async Task SignIn_IsRejectedWithoutACaptcha()
    {
        var response = await SignIn(null);
        var body = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(CaptchaRejectedResponse.InvalidCode, (string)body["code"]!);
    }

    [Fact]
    public async Task SignIn_IsRejectedWithAWrongAnswer()
    {
        var challenge = await Challenge();

        var response = await SignIn(new JsonObject { ["id"] = (string)challenge["id"]!, ["answer"] = "nope" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_ContinuesWithTheRightAnswerOnlyOnce()
    {
        var challenge = await Challenge();
        var captcha = new JsonObject { ["id"] = (string)challenge["id"]!, ["answer"] = AnswerOf(challenge) };

        var first = await SignIn(captcha.DeepClone());
        var second = await SignIn(captcha.DeepClone());

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }
}
