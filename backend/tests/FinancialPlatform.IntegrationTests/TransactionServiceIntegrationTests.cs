using System.Net;
using System.Net.Http.Json;
using FinancialPlatform.ApiGateway.Services;
using FinancialPlatform.TransactionService;
using FinancialPlatform.TransactionService.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinancialPlatform.IntegrationTests;

public class TransactionServiceIntegrationTests : IDisposable
{
    private const string JwtSecretKey = "TestSecretKey_MustBe32CharsOrMore!!";
    private const string JwtIssuer = "FinancialPlatform";

    private readonly WebApplicationFactory<TestEntry> _factory;
    private readonly HttpClient _client;

    public TransactionServiceIntegrationTests()
    {
        // Capture DB name before any lambda so it is evaluated exactly once
        var dbName = $"TxTestDb_{Guid.NewGuid()}";

        _factory = new WebApplicationFactory<TestEntry>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove all EF Core related registrations to avoid SqlServer/InMemory conflict
                    var toRemove = services
                        .Where(d => d.ServiceType.FullName != null &&
                                    (d.ServiceType.FullName.Contains("DbContext") ||
                                     d.ServiceType.FullName.Contains("DbContextOptions")))
                        .ToList();
                    foreach (var d in toRemove)
                        services.Remove(d);

                    services.AddDbContext<TransactionDbContext>(
                        options => options.UseInMemoryDatabase(dbName),
                        ServiceLifetime.Scoped,
                        ServiceLifetime.Singleton);
                });
                builder.UseSetting("ConnectionStrings:DefaultConnection", "unused");
                builder.UseSetting("Jwt:SecretKey", JwtSecretKey);
                builder.UseSetting("Jwt:Issuer", JwtIssuer);
            });
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateTestToken());
    }

    private static string GenerateTestToken(string userId = "test-user", string role = "Admin")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Jwt:SecretKey", JwtSecretKey),
                new KeyValuePair<string, string?>("Jwt:Issuer", JwtIssuer)
            })
            .Build();
        var jwtService = new JwtService(config);
        return jwtService.GenerateToken(userId, role);
    }

    [Fact]
    public async Task GetAllTransactions_EmptyDb_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/transactions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_ValidRequest_ReturnsCreated()
    {
        var request = new
        {
            UserId = "user_001",
            Amount = 150.50m,
            Currency = "USD",
            Description = "Integration test",
            Counterparty = "CP"
        };

        var response = await _client.PostAsJsonAsync("/api/transactions", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<TransactionResult>();
        Assert.NotNull(result);
        Assert.Equal("user_001", result.userId);
        Assert.Equal(150.50m, result.amount);
    }

    [Fact]
    public async Task CreateTransaction_ZeroAmount_ReturnsBadRequest()
    {
        var request = new { UserId = "user_001", Amount = 0m, Currency = "USD" };
        var response = await _client.PostAsJsonAsync("/api/transactions", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_NegativeAmount_ReturnsBadRequest()
    {
        var request = new { UserId = "user_001", Amount = -10m, Currency = "USD" };
        var response = await _client.PostAsJsonAsync("/api/transactions", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTransaction_ExistingId_ReturnsOk()
    {
        var createResp = await _client.PostAsJsonAsync("/api/transactions",
            new { UserId = "user_001", Amount = 200m, Currency = "EUR" });
        var created = await createResp.Content.ReadFromJsonAsync<TransactionResult>();
        Assert.NotNull(created);

        var getResp = await _client.GetAsync($"/api/transactions/{created.id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
    }

    [Fact]
    public async Task GetTransaction_NonexistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/transactions/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactionsByUser_ReturnsUserTransactions()
    {
        await _client.PostAsJsonAsync("/api/transactions",
            new { UserId = "user_a", Amount = 100m, Currency = "USD" });
        await _client.PostAsJsonAsync("/api/transactions",
            new { UserId = "user_b", Amount = 200m, Currency = "USD" });
        await _client.PostAsJsonAsync("/api/transactions",
            new { UserId = "user_a", Amount = 300m, Currency = "EUR" });

        var response = await _client.GetAsync("/api/transactions/user/user_a");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("user_a", body);
    }

    private record TransactionResult(string id, string userId, decimal amount, string currency,
        string status, DateTime timestamp, string? description, string? counterparty);

    public void Dispose()
    {
        _factory.Dispose();
        _client.Dispose();
    }
}
