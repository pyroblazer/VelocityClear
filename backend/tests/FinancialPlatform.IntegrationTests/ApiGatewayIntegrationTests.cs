using System.Net;
using System.Net.Http.Json;
using FinancialPlatform.ApiGateway;
using FinancialPlatform.ApiGateway.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinancialPlatform.IntegrationTests;

public class ApiGatewayIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<TestEntry> _factory;
    private readonly HttpClient _client;

    public ApiGatewayIntegrationTests()
    {
        var dbName = $"GatewayTestDb_{Guid.NewGuid()}";
        _factory = new WebApplicationFactory<TestEntry>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Jwt:SecretKey", "TestSecretKey_MustBe32CharsOrMore!!");
                builder.UseSetting("Jwt:Issuer", "TestIssuer");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "unused");
                builder.ConfigureServices(services =>
                {
                    var toRemove = services
                        .Where(d => d.ServiceType.FullName != null &&
                                    (d.ServiceType.FullName.Contains("DbContext") ||
                                     d.ServiceType.FullName.Contains("DbContextOptions")))
                        .ToList();
                    foreach (var d in toRemove)
                        services.Remove(d);

                    services.AddDbContext<GatewayDbContext>(
                        options => options.UseInMemoryDatabase(dbName),
                        ServiceLifetime.Scoped,
                        ServiceLifetime.Singleton);
                });
            });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/gateway/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { Username = "admin", Password = "admin123" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal("Admin", result.Role);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { Username = "admin", Password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_MissingFields_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_NewUser_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = "testuser_" + Guid.NewGuid(), Password = "pass123", Role = "User" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsConflict()
    {
        var username = "dup_" + Guid.NewGuid();
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = username, Password = "pass123", Role = "User" });

        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = username, Password = "different", Role = "User" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = "user_" + Guid.NewGuid(), Password = "pass123", Role = "SuperAdmin" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Status_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/gateway/status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Status_WithAuth_ReturnsOk()
    {
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "admin123" });
        var login = await loginResp.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(login);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/gateway/status");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.Token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private record LoginResult(string Token, string Role, DateTime ExpiresAt);

    public void Dispose()
    {
        _factory.Dispose();
        _client.Dispose();
    }
}
