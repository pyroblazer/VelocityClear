// ============================================================================
// RequestLoggingMiddleware.cs - HTTP Request/Response Logging Middleware
// ============================================================================
// Middleware in ASP.NET Core is a component that sits in the HTTP request
// pipeline and can inspect, modify, or short-circuit requests and responses.
// Each middleware component:
//   1. Receives the HTTP context (request + response)
//   2. Optionally performs work before calling the next middleware
//   3. Calls the next middleware in the pipeline (via RequestDelegate)
//   4. Optionally performs work after the next middleware completes
//
// The pipeline forms a chain: Request -> Middleware1 -> Middleware2 -> ...
//   -> Controller -> ... -> Middleware2 -> Middleware1 -> Response
//
// This middleware logs every incoming request (method, path, timestamp) and
// its corresponding response (status code, duration). It is registered in
// Program.cs via app.UseMiddleware<RequestLoggingMiddleware>().
// ============================================================================

using System.Diagnostics;

namespace FinancialPlatform.ApiGateway.Middleware;

public class RequestLoggingMiddleware
{
    // --------------------------------------------------------------------------
    // RequestDelegate - a function pointer representing the next middleware
    // in the pipeline. Invoking it passes the request to the next component.
    // If a middleware does not call _next(context), the pipeline is
    // "short-circuited" - no further middleware or controller runs.
    // This is useful for authorization failures, rate limiting, etc.
    // --------------------------------------------------------------------------
    private readonly RequestDelegate _next;

    // --------------------------------------------------------------------------
    // ILogger<T> - ASP.NET Core's built-in structured logging interface.
    // The type parameter T (here, RequestLoggingMiddleware) sets the
    // "category" of the logger, which appears in log output and helps filter
    // logs by source. For example, this logger's category would be:
    //   "FinancialPlatform.ApiGateway.Middleware.RequestLoggingMiddleware"
    //
    // Structured logging means you use named placeholders like {Method} rather
    // than string interpolation. This allows log sinks (Elasticsearch, Seq,
    // etc.) to index and query individual properties.
    // --------------------------------------------------------------------------
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    // --------------------------------------------------------------------------
    // Constructor - receives the next delegate and a logger via dependency
    // injection. ASP.NET Core automatically provides both when the middleware
    // is registered with UseMiddleware<T>().
    // --------------------------------------------------------------------------
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // --------------------------------------------------------------------------
    // InvokeAsync - the main middleware method. ASP.NET Core calls this for
    // every HTTP request. The method name "InvokeAsync" is the conventional
    // name that the framework looks for (alternatively, middleware can be
    // implemented as a Func<RequestDelegate, RequestDelegate>).
    //
    // HttpContext - encapsulates ALL information about a single HTTP request
    // and its response. It includes:
    //   context.Request  - method, path, headers, query string, body
    //   context.Response - status code, headers, body
    //   context.User     - authenticated user identity (from JWT, etc.)
    //   context.Items    - per-request key-value storage for sharing data
    //                       between middleware components
    // --------------------------------------------------------------------------
    public async Task InvokeAsync(HttpContext context)
    {
        // ----------------------------------------------------------------------
        // Stopwatch - a high-resolution timer from System.Diagnostics.
        // It uses the most precise timing mechanism available on the OS
        // (typically QueryPerformanceCounter on Windows, clock_gettime on Linux).
        // StartNew() creates and immediately starts the timer.
        //
        // This is preferred over DateTime for measuring elapsed time because:
        //   - It has higher resolution (nanoseconds vs milliseconds)
        //   - It is not affected by system clock changes (NTP adjustments, etc.)
        // ----------------------------------------------------------------------
        var stopwatch = Stopwatch.StartNew();

        // DateTime.UtcNow.ToString("O") formats the timestamp in ISO 8601
        // format with full precision (including fractional seconds and timezone).
        // The "O" (round-trip) format preserves all precision so the timestamp
        // can be parsed back without information loss.
        var timestamp = DateTime.UtcNow.ToString("O");

        // Extract request details from the HttpContext
        var method = context.Request.Method;   // HTTP method: "GET", "POST", "PUT", "DELETE", etc.
        var path = context.Request.Path;        // Request path: "/api/auth/login", etc.

        // ----------------------------------------------------------------------
        // LogInformation - logs at the Information severity level.
        // The "{Timestamp} {Method} {Path}" is a message template with named
        // placeholders (not string interpolation). The logger associates each
        // placeholder with the corresponding argument. Structured logging sinks
        // can then index "Method" and "Path" as separate searchable fields.
        // ----------------------------------------------------------------------
        _logger.LogInformation("Request: {Timestamp} {Method} {Path}", timestamp, method, path);

        // ----------------------------------------------------------------------
        // await _next(context) - passes control to the next middleware in the
        // pipeline. The "await" keyword suspends this method until the entire
        // downstream pipeline (including the controller) finishes processing.
        // This is how we measure the total request processing time.
        // ----------------------------------------------------------------------
        await _next(context);

        // Stop the timer - elapsed time now reflects total processing duration
        stopwatch.Stop();

        // Read the response status code (200, 404, 500, etc.) and duration
        var statusCode = context.Response.StatusCode;
        var duration = stopwatch.ElapsedMilliseconds;  // Duration in milliseconds (as a long/int64)

        // Log the response details including how long the request took
        _logger.LogInformation(
            "Response: {Timestamp} {Method} {Path} - Status: {StatusCode} - Duration: {Duration}ms",
            DateTime.UtcNow.ToString("O"), method, path, statusCode, duration);
    }
}
