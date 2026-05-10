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

public class ApiKeyIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<TestEntry> _factory;
    private readonly HttpClient _client;

    public ApiKeyIntegrationTests()
    {
        var dbName = $"ApiKeyTestDb_{Guid.NewGuid()}";
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

    private async Task<string> GetAdminTokenAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "admin123" });
        var result = await resp.Content.ReadFromJsonAsync<LoginResult>();
        return result!.Token;
    }

    private HttpRequestMessage AuthRequest(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    [Fact]
    public async Task ListKeys_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/apikeys");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateKey_WithAdminAuth_ReturnsCreated()
    {
        var token = await GetAdminTokenAsync();
        var request = AuthRequest(HttpMethod.Post, "/api/apikeys", token);
        request.Content = JsonContent.Create(new
        {
            Name = "Integration Test Key",
            Permissions = new[] { "transactions:read", "transactions:write" }
        });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateKeyResult>();
        Assert.NotNull(result);
        Assert.StartsWith("vc_live_", result.ApiKey);
        Assert.Equal("Integration Test Key", result.Name);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task ListKeys_WithAdminAuth_ReturnsOk()
    {
        var token = await GetAdminTokenAsync();

        var createReq = AuthRequest(HttpMethod.Post, "/api/apikeys", token);
        createReq.Content = JsonContent.Create(new { Name = "List Test", Permissions = new[] { "audit:read" } });
        await _client.SendAsync(createReq);

        var listReq = AuthRequest(HttpMethod.Get, "/api/apikeys", token);
        var response = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var keys = await response.Content.ReadFromJsonAsync<List<KeyResult>>();
        Assert.NotNull(keys);
        Assert.NotEmpty(keys);
    }

    [Fact]
    public async Task RevokeKey_SetsInactive()
    {
        var token = await GetAdminTokenAsync();

        var createReq = AuthRequest(HttpMethod.Post, "/api/apikeys", token);
        createReq.Content = JsonContent.Create(new { Name = "Revoke Test", Permissions = new[] { "transactions:read" } });
        var createResp = await _client.SendAsync(createReq);
        var created = await createResp.Content.ReadFromJsonAsync<CreateKeyResult>();
        Assert.NotNull(created);

        var deleteReq = AuthRequest(HttpMethod.Delete, $"/api/apikeys/{created.Id}", token);
        var deleteResp = await _client.SendAsync(deleteReq);
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var listReq = AuthRequest(HttpMethod.Get, "/api/apikeys", token);
        var listResp = await _client.SendAsync(listReq);
        var keys = await listResp.Content.ReadFromJsonAsync<List<KeyResult>>();
        var revoked = keys!.First(k => k.Id == created.Id);
        Assert.False(revoked.IsActive);
    }

    [Fact]
    public async Task RevokeKey_NonExistent_ReturnsNotFound()
    {
        var token = await GetAdminTokenAsync();
        var deleteReq = AuthRequest(HttpMethod.Delete, "/api/apikeys/nonexistent-id", token);
        var response = await _client.SendAsync(deleteReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateKey_GeneratesUniqueKeysEachTime()
    {
        var token = await GetAdminTokenAsync();

        var req1 = AuthRequest(HttpMethod.Post, "/api/apikeys", token);
        req1.Content = JsonContent.Create(new { Name = "Key A", Permissions = new[] { "transactions:read" } });
        var resp1 = await _client.SendAsync(req1);
        var key1 = await resp1.Content.ReadFromJsonAsync<CreateKeyResult>();

        var req2 = AuthRequest(HttpMethod.Post, "/api/apikeys", token);
        req2.Content = JsonContent.Create(new { Name = "Key B", Permissions = new[] { "transactions:read" } });
        var resp2 = await _client.SendAsync(req2);
        var key2 = await resp2.Content.ReadFromJsonAsync<CreateKeyResult>();

        Assert.NotEqual(key1!.ApiKey, key2!.ApiKey);
    }

    private record LoginResult(string Token, string Role, DateTime ExpiresAt);
    private record CreateKeyResult(string Id, string Name, string ApiKey, string KeyPrefix, string[] Permissions, DateTime CreatedAt, bool IsActive);
    private record KeyResult(string Id, string Name, string KeyPrefix, string[] Permissions, DateTime CreatedAt, bool IsActive);

    public void Dispose()
    {
        _factory.Dispose();
        _client.Dispose();
    }
}
