using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FinancialPlatform.ApiGateway;
using FinancialPlatform.ApiGateway.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FinancialPlatform.IntegrationTests;

public class SecurityTests : IDisposable
{
    private readonly WebApplicationFactory<TestEntry> _factory;
    private readonly HttpClient _client;
    private const string JwtSecretKey = "TestSecretKey_MustBe32CharsOrMore!!";
    private const string JwtIssuer = "FinancialPlatform";

    public SecurityTests()
    {
        _factory = new WebApplicationFactory<TestEntry>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Jwt:SecretKey", JwtSecretKey);
                builder.UseSetting("Jwt:Issuer", JwtIssuer);
            });
        _client = _factory.CreateClient();
    }

    private string GenerateToken(string userId, string role, int expiryHours = 1)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Jwt:SecretKey", JwtSecretKey),
                new KeyValuePair<string, string?>("Jwt:Issuer", JwtIssuer)
            })
            .Build();
        var service = new JwtService(config);
        // JwtService generates tokens with 1-hour expiry. For expired tokens,
        // we build one manually below.
        if (expiryHours == 1)
            return service.GenerateToken(userId, role);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: JwtIssuer, audience: JwtIssuer, claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task ProtectedEndpoint_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/api/gateway/status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_ValidToken_Returns200()
    {
        var token = GenerateToken("user-1", "Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/gateway/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_InvalidScheme_Returns401()
    {
        var token = GenerateToken("user-1", "Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);

        var response = await _client.GetAsync("/api/gateway/status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_TamperedToken_Returns401()
    {
        var token = GenerateToken("user-1", "Admin");
        // Tamper with the token by changing a character
        var chars = token.ToCharArray();
        chars[10] = chars[10] == 'a' ? 'b' : 'a';
        var tamperedToken = new string(chars);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        var response = await _client.GetAsync("/api/gateway/status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserToken_OnAdminEndpoint_Returns403()
    {
        var token = GenerateToken("user-1", "User");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/gateway/status");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuditorToken_OnAdminEndpoint_Returns200()
    {
        var token = GenerateToken("user-1", "Auditor");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/gateway/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GuestToken_OnProtectedEndpoint_Returns403()
    {
        var token = GenerateToken("guest-1", "Guest");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/gateway/status");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var token = GenerateToken("user-1", "Admin", expiryHours: -1);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/gateway/status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithBcryptHash_Works()
    {
        // Verify that the seeded BCrypt hashed passwords still work for login
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "admin123" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(result);
        Assert.Equal("Admin", result.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "admin123_wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_HashesPassword_WithBcrypt()
    {
        var uniqueUser = $"security-test-{Guid.NewGuid():N}"[..20];
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = uniqueUser, Password = "MyTestPass123!", Role = "User" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Login with the registered user should work
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new { Username = uniqueUser, Password = "MyTestPass123!" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private record LoginResult(string Token, string Role, DateTime ExpiresAt);
}
