using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinancialPlatform.ApiGateway.Data;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ApiGateway.Controllers;

[ApiController]
[Route("api/apikeys")]
public class ApiKeysController : ControllerBase
{
    private readonly GatewayDbContext _dbContext;
    private readonly ILogger<ApiKeysController> _logger;

    public ApiKeysController(GatewayDbContext dbContext, ILogger<ApiKeysController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Lists all API keys.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<ApiKeyResponse>>> GetAll()
    {
        var keys = await _dbContext.ApiKeys
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync();

        var responses = keys.Select(k => new ApiKeyResponse
        {
            Id = k.Id,
            Name = k.Name,
            KeyPrefix = k.KeyPrefix,
            Permissions = JsonSerializer.Deserialize<string[]>(k.Permissions) ?? [],
            CreatedAt = k.CreatedAt,
            LastUsedAt = k.LastUsedAt,
            IsActive = k.IsActive
        });

        return Ok(responses);
    }

    /// <summary>
    /// Generates a new API key. The plaintext key is returned only in this response.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CreateApiKeyResponse>> Create([FromBody] CreateApiKeyRequest request)
    {
        // Generate a cryptographically random key with the vc_live_ prefix
        var rawKeyBytes = RandomNumberGenerator.GetBytes(32);
        var rawKey = $"vc_live_{Convert.ToHexString(rawKeyBytes).ToLowerInvariant()}";

        // SHA-256 hash for storage
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        var keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            KeyHash = keyHash,
            KeyPrefix = rawKey[..12], // Store "vc_live_xxxx" prefix for identification
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "admin",
            Permissions = JsonSerializer.Serialize(request.Permissions),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            IsActive = true
        };

        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("API key created: {KeyPrefix} ({Name})", apiKey.KeyPrefix, apiKey.Name);

        var response = new CreateApiKeyResponse
        {
            Id = apiKey.Id,
            Name = apiKey.Name,
            KeyPrefix = apiKey.KeyPrefix,
            Permissions = request.Permissions,
            CreatedAt = apiKey.CreatedAt,
            LastUsedAt = apiKey.LastUsedAt,
            IsActive = apiKey.IsActive,
            ApiKey = rawKey
        };

        return CreatedAtAction(nameof(GetAll), new { id = apiKey.Id }, response);
    }

    /// <summary>
    /// Soft-deletes an API key by setting IsActive to false.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var apiKey = await _dbContext.ApiKeys.FindAsync(id);
        if (apiKey is null)
        {
            return NotFound(new { message = "API key not found" });
        }

        apiKey.IsActive = false;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("API key deactivated: {KeyPrefix} ({Name})", apiKey.KeyPrefix, apiKey.Name);

        return NoContent();
    }
}
