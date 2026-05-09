using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinancialPlatform.ApiGateway.Controllers;
using FinancialPlatform.ApiGateway.Data;
using FinancialPlatform.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class ApiKeyServiceTests
{
    private readonly GatewayDbContext _db;
    private readonly Mock<ILogger<ApiKeysController>> _logger;
    private readonly ApiKeysController _controller;

    public ApiKeyServiceTests()
    {
        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase($"ApiKeyTest_{Guid.NewGuid()}")
            .Options;
        _db = new GatewayDbContext(options);
        _logger = new Mock<ILogger<ApiKeysController>>();
        _controller = new ApiKeysController(_db, _logger.Object);

        // Set up a mock authenticated user
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, "test-admin"),
            new(System.Security.Claims.ClaimTypes.Role, "Admin")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(identity)
            }
        };
    }

    [Fact]
    public async Task Create_GeneratesKeyWithCorrectPrefix()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "Test Key",
            Permissions = ["transactions:read"]
        };

        var result = await _controller.Create(request);
        var created = result.Result as CreatedAtActionResult;
        Assert.NotNull(created);

        var response = created.Value as CreateApiKeyResponse;
        Assert.NotNull(response);
        Assert.StartsWith("vc_live_", response.ApiKey);
        Assert.Equal("Test Key", response.Name);
        Assert.Contains("transactions:read", response.Permissions);
        Assert.True(response.IsActive);
    }

    [Fact]
    public async Task Create_StoresHashedKey_NotPlaintext()
    {
        var request = new CreateApiKeyRequest { Name = "Hash Test", Permissions = ["audit:read"] };
        var result = await _controller.Create(request);
        var created = result.Result as CreatedAtActionResult;
        var response = created!.Value as CreateApiKeyResponse;

        // The stored hash should be the SHA-256 of the plaintext key
        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(response!.ApiKey))).ToLowerInvariant();

        var dbKey = await _db.ApiKeys.FirstAsync();
        Assert.Equal(expectedHash, dbKey.KeyHash);
        Assert.NotEqual(response.ApiKey, dbKey.KeyHash); // Not storing plaintext
    }

    [Fact]
    public async Task Create_StoresKeyPrefixForIdentification()
    {
        var request = new CreateApiKeyRequest { Name = "Prefix Test", Permissions = [] };
        var result = await _controller.Create(request);
        var created = result.Result as CreatedAtActionResult;
        var response = created!.Value as CreateApiKeyResponse;

        var dbKey = await _db.ApiKeys.FirstAsync();
        Assert.Equal(12, dbKey.KeyPrefix.Length); // "vc_live_xxxx" = 12 chars
        Assert.StartsWith(dbKey.KeyPrefix, response!.ApiKey);
    }

    [Fact]
    public async Task GetAll_ReturnsAllKeys()
    {
        _db.ApiKeys.AddRange(
            new FinancialPlatform.Shared.Models.ApiKey { Id = "1", Name = "Key A", KeyHash = "hash1", KeyPrefix = "vc_live_a1", Permissions = "[]", CreatedAt = DateTime.UtcNow, IsActive = true },
            new FinancialPlatform.Shared.Models.ApiKey { Id = "2", Name = "Key B", KeyHash = "hash2", KeyPrefix = "vc_live_b2", Permissions = "[]", CreatedAt = DateTime.UtcNow, IsActive = false }
        );
        await _db.SaveChangesAsync();

        var result = await _controller.GetAll();
        var ok = result.Result as OkObjectResult;
        Assert.NotNull(ok);

        var keys = (ok.Value as IEnumerable<ApiKeyResponse>)!.ToList();
        Assert.Equal(2, keys.Count);
    }

    [Fact]
    public async Task Delete_SetsKeyInactive()
    {
        var apiKey = new FinancialPlatform.Shared.Models.ApiKey
        {
            Id = "del-test",
            Name = "Delete Me",
            KeyHash = "hash",
            KeyPrefix = "vc_live_xx",
            Permissions = "[]",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync();

        var result = await _controller.Delete("del-test");
        Assert.IsType<NoContentResult>(result);

        var dbKey = await _db.ApiKeys.FindAsync("del-test");
        Assert.False(dbKey!.IsActive);
    }

    [Fact]
    public async Task Delete_NonExistentKey_ReturnsNotFound()
    {
        var result = await _controller.Delete("nonexistent");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Create_GeneratesUniqueKeys()
    {
        var request = new CreateApiKeyRequest { Name = "Unique Test", Permissions = [] };

        var result1 = await _controller.Create(request);
        var result2 = await _controller.Create(request);

        var resp1 = (result1.Result as CreatedAtActionResult)!.Value as CreateApiKeyResponse;
        var resp2 = (result2.Result as CreatedAtActionResult)!.Value as CreateApiKeyResponse;

        Assert.NotEqual(resp1!.ApiKey, resp2!.ApiKey);
        Assert.NotEqual(resp1.Id, resp2.Id);
    }
}
