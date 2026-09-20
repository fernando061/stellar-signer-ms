using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using StellarDotnetSdk;
namespace StellarSigner.SecurityTests;
public sealed class ApiAuthenticationTests
{
    [Fact]
    public async Task InternalEndpointsRequireBearerTokenAndLivenessIsPublic()
    {
        var values = new Dictionary<string, string?>
        {
            ["SIGNER_WRAP_KEY"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            ["SIGNER_MASTER_KEY_FILE"] = Path.Combine(Path.GetTempPath(), "no-master-payload-for-auth-test"),
            ["ConnectionStrings__SignerDb"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
            ["Jwt__Authority"] = "https://issuer.invalid",
            ["Jwt__Audience"] = "stellar-signer",
            ["Stellar__Issuer"] = "GDRXE2BQUC3AZNPVFSCEZ76NJ3WWL25FYFK6RGZGIEKWE4SOOHSUJUJ6",
            ["Stellar__ContractId"] = StrKey.EncodeContractId(new byte[32])
        };
        var previous = values.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        foreach (var pair in values) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        try
        {
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host => host.UseEnvironment("Development"));
            var client = factory.CreateClient();
            Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized,
                (await client.GetAsync("/api/internal/v1/wallets/00000000-0000-0000-0000-000000000001")).StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized,
                (await client.PostAsync("/api/internal/v1/signatures", new StringContent("{}"))).StatusCode);
            await using var scopedFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            {
                host.UseEnvironment("Development");
                host.ConfigureTestServices(services => services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { }));
            });
            var scopedClient = scopedFactory.CreateClient();
            async Task<System.Net.HttpStatusCode> WalletPost(string scope)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/v1/wallets")
                { Content = new StringContent("{\"partnerId\":\"00000000-0000-0000-0000-000000000000\"}", Encoding.UTF8, "application/json") };
                request.Headers.Add("X-Test-Scope", scope);
                return (await scopedClient.SendAsync(request)).StatusCode;
            }
            Assert.Equal(System.Net.HttpStatusCode.Forbidden, await WalletPost("wrong-scope"));
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, await WalletPost("stellar-signer.execute"));
        }
        finally
        {
            foreach (var pair in previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }
    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Scope", out var scope))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim("scope", scope.ToString()), new Claim("sub", "test-service")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
