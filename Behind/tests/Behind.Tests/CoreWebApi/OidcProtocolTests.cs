using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Oidc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Behind.Tests.CoreWebApi;

public sealed class OidcProtocolTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task BrowserFlowValidatesTokenAndNonceBeforeIssuingBoundTicket(
        bool validNonce, bool validSignature, bool globallyEnabled)
    {
        using var rsa = RSA.Create(2048);
        using var wrongRsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "provider-key" };
        var wrongKey = new RsaSecurityKey(wrongRsa) { KeyId = "provider-key" };
        using var certificateKey = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", certificateKey, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:Url"] = "https://frontend.example.test/",
            ["Security:Oidc:Providers:0:Id"] = "company",
            ["Security:Oidc:Providers:0:DisplayName"] = "Company",
            ["Security:Oidc:Providers:0:Authority"] = "https://provider.example.test",
            ["Security:Oidc:Providers:0:ClientId"] = "behind"
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.Configure<AccountsConfiguration>(builder.Configuration.GetSection(AccountsConfiguration.SectionName));
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddSingleton<IOptions<SecurityConfiguration>>(Options.Create(
            new SecurityConfiguration(false, None, Some(new JwtSettings("behind", "frontend", new string('k', 64))), None)));
        builder.AddOidc();
        var token = new TokenEndpoint();
        builder.Services.Configure<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>(
            OidcProviderCatalog.Scheme("company"), options =>
            {
                var configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://provider.example.test",
                    AuthorizationEndpoint = "https://provider.example.test/authorize",
                    TokenEndpoint = "https://provider.example.test/token"
                };
                configuration.SigningKeys.Add(key);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                options.Backchannel = new HttpClient(token);
            });
        await using var application = builder.Build();
        application.UseAuthentication();
        application.MapGet("/sign/in/oidc/{id}", OidcSignInEndpoints.Challenge);
        await application.StartAsync();
        try
        {
            var address = application.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CookieContainer = new CookieContainer(),
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
            using var challenge = await client.GetAsync("/sign/in/oidc/company");
            Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
            var parameters = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
            Assert.Equal("code", parameters["response_type"]);
            Assert.Equal("S256", parameters["code_challenge_method"]);
            Assert.False(string.IsNullOrEmpty(parameters["code_challenge"]));
            if (!globallyEnabled)
            {
                builder.Configuration[$"{AccountsConfiguration.SectionName}:Oidc"] = "false";
                ((IConfigurationRoot)builder.Configuration).Reload();
            }
            token.IdToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                issuer: "https://provider.example.test", audience: "behind",
                claims:
                [
                    new Claim("sub", "provider-subject"),
                    new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                        ClaimValueTypes.Integer64),
                    new Claim("nonce", validNonce ? parameters["nonce"].ToString() : "wrong-nonce"),
                    new Claim("email", "user@example.test"),
                    new Claim("email_verified", "true", ClaimValueTypes.Boolean)
                ],
                notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(validSignature ? key : wrongKey, SecurityAlgorithms.RsaSha256)));
            using var callback = await client.PostAsync("/sign/in/oidc/callback/company", new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["code"] = "provider-code",
                    ["state"] = parameters["state"].ToString()
                }));
            Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
            Assert.Equal("frontend.example.test", callback.Headers.Location!.Host);
            var fragment = callback.Headers.Location.Fragment;
            if (validNonce && validSignature && globallyEnabled)
            {
                Assert.StartsWith("#/signin?oidc=", fragment);
                var code = QueryHelpers.ParseQuery(fragment["#/signin".Length..])["oidc"].ToString();
                var cookies = handler.CookieContainer.GetCookies(new Uri(address + "/sign/in/oidc/redeem"));
                var ticket = application.Services.GetRequiredService<OidcTicketStore>().Redeem(code,
                    flow => cookies[OidcExtensions.CookieName(flow)]?.Value);
                Assert.NotNull(ticket);
                Assert.Equal("provider-subject", ticket.Identity.Subject);
                Assert.True(ticket.Identity.EmailVerified);
                Assert.DoesNotContain(token.IdToken, fragment);
                Assert.Contains("code_verifier=", token.TokenRequest);
                var verifier = QueryHelpers.ParseQuery(token.TokenRequest)["code_verifier"].ToString();
                Assert.Equal(parameters["code_challenge"].ToString(),
                    Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
                using var replay = await client.PostAsync("/sign/in/oidc/callback/company", new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["code"] = "provider-code",
                        ["state"] = parameters["state"].ToString()
                    }));
                Assert.Equal("#/signin?oidcError=failed", replay.Headers.Location!.Fragment);
            }
            else
            {
                Assert.Equal("#/signin?oidcError=failed", fragment);
            }
        }
        finally
        {
            await application.StopAsync();
        }
    }

    private sealed class TokenEndpoint : HttpMessageHandler
    {
        public string IdToken { get; set; } = string.Empty;
        public string TokenRequest { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://provider.example.test/token", request.RequestUri!.AbsoluteUri);
            TokenRequest = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "provider-access-token", token_type = "Bearer", expires_in = 300, id_token = IdToken
                }), Encoding.UTF8, "application/json")
            };
        }
    }
}
