// ============================================================================
// Program.cs - Application Entry Point
// ============================================================================
// This is the startup file for the API Gateway microservice. In ASP.NET Core,
// this file uses "top-level statements" - meaning there is no explicit Main()
// method or class wrapping. The compiler generates a Main() method behind the
// scenes. This is the default style for .NET 6+ projects.
//
// The API Gateway acts as a single entry point for all frontend clients.
// It handles JWT authentication, routes requests to backend microservices,
// broadcasts real-time events via Server-Sent Events (SSE), and logs every
// incoming HTTP request through custom middleware.
// ============================================================================

// "using" statements import namespaces, similar to "import" in Java/Python
// or "#include" in C++. They make types available without fully qualifying them.
using System.Text;
using FinancialPlatform.ApiGateway.Middleware;
using FinancialPlatform.ApiGateway.Services;
using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.EventInfrastructure.Sse;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;

// --------------------------------------------------------------------------
// Serilog: Structured Logging Setup
// --------------------------------------------------------------------------
// Serilog is a popular third-party logging library for .NET. Unlike plain
// text logging, Serilog supports "structured" logs - log messages can have
// named properties that are machine-readable (useful for searching in
// Elasticsearch, Seq, etc.).
//
// Log.Logger is a static global logger instance. We configure it here before
// anything else so that even startup errors get logged.
// --------------------------------------------------------------------------
// Set up a console-only logger before the host is built so startup errors
// are captured. CreateLogger() (not CreateBootstrapLogger()) avoids the
// ReloadableLogger freeze issue when multiple WebApplicationFactory instances
// run in the same test process.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting ApiGateway host");

    // ----------------------------------------------------------------------
    // WebApplication Builder
    // ----------------------------------------------------------------------
    // WebApplication.CreateBuilder(args) creates a "builder" object that
    // collects all configuration, services, and settings needed to create
    // the web application. "args" are command-line arguments.
    //
    // "var" is C#'s type inference keyword (like "auto" in C++). The compiler
    // figures out the type from the right-hand side. Here, "builder" will be
    // of type WebApplicationBuilder.
    // ----------------------------------------------------------------------
    var builder = WebApplication.CreateBuilder(args);

    // Replace the default .NET logger with Serilog using the static Log.Logger
    // configured above (console-only, no file sink — Factor XI: treat logs as streams).
    builder.Host.UseSerilog();

    // ----------------------------------------------------------------------
    // JWT Authentication Configuration
    // ----------------------------------------------------------------------
    // JWT (JSON Web Token) is a standard for authentication. After login,
    // the server issues a signed token. The client sends it with every
    // request (typically in the Authorization header), and the server
    // validates the signature to confirm identity.
    //
    // builder.Configuration["key"] reads values from appsettings.json,
    // environment variables, or command-line args. The "??" operator is
    // the "null-coalescing operator" - it returns the left side if non-null,
    // otherwise the right side (similar to "left ?? right" in TypeScript/JS).
    // ----------------------------------------------------------------------
    var secretKey = builder.Configuration["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("Jwt:SecretKey is not configured. Set it via environment variable or appsettings.");
    if (secretKey.Length < 32)
        throw new InvalidOperationException("Jwt:SecretKey must be at least 32 characters for HMAC-SHA256 security.");
    var issuer = builder.Configuration["Jwt:Issuer"] ?? "FinancialPlatform";

    // AddAuthentication registers the authentication system. The parameter
    // sets the default scheme to JWT Bearer tokens.
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // TokenValidationParameters tell ASP.NET how to validate incoming tokens.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,                    // Check the "iss" claim matches
                ValidateAudience = true,                  // Check the "aud" claim matches
                ValidateLifetime = true,                  // Check the token hasn't expired
                ValidateIssuerSigningKey = true,          // Verify the cryptographic signature
                ValidIssuer = issuer,                     // Expected issuer value
                ValidAudience = issuer,                   // Expected audience value
                // SymmetricSecurityKey: The same secret key is used for both
                // signing and verifying the token (HMAC). The key must be at
                // least 128 bits (16 bytes) for HMAC-SHA256.
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
            };
        });

    // AddAuthorization registers the authorization policy system. Combined
    // with [Authorize] attributes on controllers, this enforces access control.
    builder.Services.AddAuthorization();

    // ----------------------------------------------------------------------
    // Dependency Injection (DI) Service Registration
    // ----------------------------------------------------------------------
    // ASP.NET Core has a built-in Inversion of Control (IoC) container.
    // You register services here, and they can be injected into controllers
    // and other classes via constructor parameters.
    //
    // AddSingleton<TInterface, TImplementation>():
    //   Creates ONE instance of TImplementation and reuses it for every
    //   request. This is appropriate for stateless services or shared state.
    //
    // Other lifetimes:
    //   AddTransient  - new instance every time it's requested
    //   AddScoped     - one instance per HTTP request
    //   AddSingleton  - one instance for the entire application lifetime
    // ----------------------------------------------------------------------
    builder.Services.AddSingleton<IEventBus, AdaptiveEventBus>();    // Event bus for pub/sub messaging
    builder.Services.AddSingleton<ISseHub, InMemorySseHub>();        // Server-Sent Events hub
    builder.Services.AddSingleton<JwtService>();                     // JWT token generation service
    builder.Services.AddHttpClient();                                 // IHttpClientFactory for outbound HTTP calls

    // AddControllers() registers support for MVC-style controllers.
    // It scans the assembly for classes with [ApiController] and maps routes.
    builder.Services.AddControllers();

    // -- Swagger / OpenAPI --
    builder.Services.AddOpenApi();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddMetrics();

    // Rate limiting configuration (ISO 27001 A.12.6.1)
    builder.Services.Configure<RateLimitingOptions>(
        builder.Configuration.GetSection("RateLimiting"));

    // Build the Application Pipeline
    // ----------------------------------------------------------------------
    // builder.Build() creates the WebApplication from all the registered
    // services and configuration. Everything before this adds to the builder;
    // everything after this configures the HTTP request pipeline.
    // ----------------------------------------------------------------------
    var app = builder.Build();

    // Swagger UI available in all environments (not just Development) so that
    // Docker deployments can also use interactive API documentation.
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "API Gateway v1");
        options.RoutePrefix = "swagger";
    });

    // ----------------------------------------------------------------------
    // Middleware Pipeline
    // ----------------------------------------------------------------------
    // Middleware are components that form a pipeline through which every HTTP
    // request passes. Each middleware can:
    //   1. Do something before passing the request to the next middleware
    //   2. Call the next middleware (via await _next(context))
    //   3. Do something after the next middleware finishes
    //
    // ORDER MATTERS - middleware runs in the order it is registered.
    //
    // UseAuthentication() inspects the request for credentials (e.g., JWT
    //   in the Authorization header) and populates the User identity.
    // UseAuthorization() checks whether the authenticated user is allowed
    //   to access the requested endpoint (based on [Authorize] attributes).
    // ----------------------------------------------------------------------
    app.UseMiddleware<RateLimitingMiddleware>();         // Rate limit before auth
    app.UseAuthentication();                            // Must come before UseAuthorization
    app.UseAuthorization();                             // Must come after UseAuthentication

    // UseMiddleware<T>() adds our custom middleware to the pipeline.
    // RequestLoggingMiddleware logs every request and its response status/duration.
    app.UseMiddleware<RequestLoggingMiddleware>();

    // MapControllers() maps attribute-routed controllers (those with [Route]
    // and [HttpGet]/[HttpPost] attributes) to their endpoints.
    app.MapControllers();
    app.MapMetrics();

    var eventBus = app.Services.GetRequiredService<IEventBus>();
    if (eventBus is AdaptiveEventBus adaptiveBus)
    {
        FinancialPlatform.ApiGateway.ServiceMetrics.EventBusCurrentBackend.Set((double)adaptiveBus.CurrentBackend);
        adaptiveBus.BackendChanged += (_, backend) => FinancialPlatform.ApiGateway.ServiceMetrics.EventBusCurrentBackend.Set((double)backend);
    }

    app.Run("http://0.0.0.0:5000");
}
catch (Exception ex)
{
    // Log.Fatal logs at the highest severity level - used for unrecoverable errors.
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    // CloseAndFlush ensures any buffered log entries are written before the
    // process exits. Without this, the last few log entries could be lost.
    Log.CloseAndFlush();
}

// --------------------------------------------------------------------------
// partial class Program
// --------------------------------------------------------------------------
// "partial class" allows a single class to be split across multiple files.
// The compiler merges all partial definitions into one class at build time.
//
// This declaration exists so that other files (like test projects or the
// middleware) can reference the implicitly-generated "Program" class that
// top-level statements create. Without this, the class is internal to the
// compilation and not accessible elsewhere.
// --------------------------------------------------------------------------
public partial class Program { }
