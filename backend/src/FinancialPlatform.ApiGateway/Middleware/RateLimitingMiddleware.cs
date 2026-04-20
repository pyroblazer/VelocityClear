using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace FinancialPlatform.ApiGateway.Middleware;

public class RateLimitingOptions
{
    public int GeneralMaxRequests { get; set; } = 100;
    public int GeneralWindowSeconds { get; set; } = 60;
    public int LoginMaxRequests { get; set; } = 5;
    public int LoginWindowSeconds { get; set; } = 60;
}

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitingOptions _options;
    private readonly ConcurrentDictionary<string, List<DateTime>> _generalRequests = new();
    private readonly ConcurrentDictionary<string, List<DateTime>> _loginRequests = new();
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IOptions<RateLimitingOptions> options,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = context.Request.Path.Value ?? "";

        var isLogin = path.Contains("/api/auth/login", StringComparison.OrdinalIgnoreCase);
        var maxRequests = isLogin ? _options.LoginMaxRequests : _options.GeneralMaxRequests;
        var windowSeconds = isLogin ? _options.LoginWindowSeconds : _options.GeneralWindowSeconds;
        var store = isLogin ? _loginRequests : _generalRequests;

        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-windowSeconds);

        var requests = store.AddOrUpdate(
            clientIp,
            _ => new List<DateTime> { now },
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.RemoveAll(t => t < cutoff);
                    existing.Add(now);
                    return existing;
                }
            });

        int count;
        lock (requests) { count = requests.Count; }

        if (count > maxRequests)
        {
            _logger.LogWarning("Rate limit exceeded for {ClientIp} on {Path}: {Count} requests in {Window}s",
                clientIp, path, count, windowSeconds);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = windowSeconds.ToString();
            await context.Response.WriteAsJsonAsync(new { message = "Too many requests. Please try again later." });
            return;
        }

        await _next(context);
    }
}
