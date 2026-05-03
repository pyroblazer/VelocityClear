using System.Security.Claims;
using System.Text;
using FinancialPlatform.ApiGateway.Data;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ApiGateway.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(
        RequestDelegate next,
        IServiceScopeFactory scopeFactory,
        ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check for the X-API-Key header
        if (!context.Request.Headers.TryGetValue("X-API-Key", out var providedKey))
        {
            // No API key header present — fall through to JWT auth
            await _next(context);
            return;
        }

        var apiKey = providedKey.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await _next(context);
            return;
        }

        // Hash the provided key to look it up
        var hashBytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        var keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // Resolve scoped DbContext via IServiceScopeFactory since middleware is singleton
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

        var storedKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive);

        if (storedKey is null)
        {
            _logger.LogWarning("API key authentication failed: key not found or inactive");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid API key" });
            return;
        }

        if (storedKey.ExpiresAt.HasValue && storedKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("API key authentication failed: key expired ({KeyPrefix})", storedKey.KeyPrefix);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "API key has expired" });
            return;
        }

        // Update last used timestamp
        storedKey.LastUsedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        // Set the HttpContext.User to a ClaimsPrincipal with the user ID and "ApiKey" role
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, storedKey.UserId),
            new(ClaimTypes.Role, "ApiKey"),
            new("ApiKeyPermissions", storedKey.Permissions)
        };

        var identity = new ClaimsIdentity(claims, "ApiKey");
        context.User = new ClaimsPrincipal(identity);

        _logger.LogInformation("API key authenticated: {KeyPrefix} for user {UserId}", storedKey.KeyPrefix, storedKey.UserId);

        await _next(context);
    }
}
