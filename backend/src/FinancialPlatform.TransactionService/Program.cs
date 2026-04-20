// ============================================================================
// Program.cs - Transaction Service Entry Point
// ============================================================================
// This is the startup file for the Transaction Service microservice. In .NET,
// Program.cs is the conventional entry point where you configure the "host"
// (the web server), register services in the Dependency Injection (DI) container,
// and set up the middleware pipeline (how HTTP requests are processed).
//
// Key concepts in this file:
//   - "Builder" pattern: WebApplication.CreateBuilder(args) creates a configurable
//     host builder. You add services to it, then call .Build() to produce the app.
//   - Dependency Injection (DI): builder.Services.AddSingleton/AddScoped/AddTransient
//     registers components so they can be injected into constructors automatically.
//   - Middleware: app.UseSerilogRequestLogging(), app.MapControllers() etc. define
//     how incoming HTTP requests are handled in order.
// ============================================================================

using System.Text;
using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.EventInfrastructure.Configuration;
using FinancialPlatform.EventInfrastructure.Sse;
using FinancialPlatform.Shared.Interfaces;
using FinancialPlatform.TransactionService.Data;
using FinancialPlatform.TransactionService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;

// Set up a console-only logger before the host is built so that early startup
// errors are captured. CreateLogger() (not CreateBootstrapLogger()) avoids the
// ReloadableLogger freeze issue that occurs when multiple WebApplicationFactory
// instances run in the same test process.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Transaction Service");

    // WebApplication.CreateBuilder(args) creates the host builder.
    // "args" are command-line arguments passed when running the app.
    var builder = WebApplication.CreateBuilder(args);

    // -- Serilog (Structured Logging) --
    // Replaces the default .NET logger with Serilog, reading its configuration
    // (sinks, minimum level, enrichers) from appsettings.json.
    builder.Host.UseSerilog((context, config) =>
    {
        config.ReadFrom.Configuration(context.Configuration);
    });

    // -- Kestrel (Web Server) --
    // Kestrel is .NET's built-in cross-platform web server. Here we configure it
    // to listen on port 5001 on all network interfaces (0.0.0.0).
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(5001);
    });

    // -- DbContext (Entity Framework Core) --
    // AddDbContext<T>() registers the database context in the DI container.
    // "Scoped" lifetime means one instance per HTTP request - this is the correct
    // lifetime for database contexts because it ensures connections are not shared
    // between concurrent requests.
    // UseSqlServer() tells EF Core to use SQL Server as the database provider,
    // with the connection string read from configuration (appsettings.json).
    builder.Services.AddDbContext<TransactionDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // -- Event Bus (Singleton) --
    // AddSingleton<TInterface, TImplementation>() registers one shared instance
    // that lives for the entire lifetime of the application. All classes that
    // request IEventBus will receive the same AdaptiveEventBus instance.
    builder.Services.AddSingleton<IEventBus>(sp =>
    {
        var config = new EventBusConnectionConfig(
            DefaultBackend: builder.Configuration["EventBus:DefaultBackend"] ?? "InMemory",
            RedisUrl: builder.Configuration["EventBus:RedisUrl"] ?? "localhost:6379",
            RabbitMqUrl: builder.Configuration["EventBus:RabbitMqUrl"] ?? "amqp://guest:guest@localhost:5672",
            KafkaBrokers: builder.Configuration["EventBus:KafkaBrokers"] ?? "localhost:9092",
            ServiceName: builder.Configuration["EventBus:ServiceName"] ?? "transaction-service"
        );
        return new AdaptiveEventBus(
            config,
            sp.GetRequiredService<ILogger<AdaptiveEventBus>>(),
            sp.GetRequiredService<ILogger<InMemoryEventBus>>(),
            sp.GetRequiredService<ILogger<RedisEventBus>>(),
            sp.GetRequiredService<ILogger<RabbitMQEventBus>>(),
            sp.GetRequiredService<ILogger<KafkaEventBus>>());
    });

    // -- SSE Hub (Singleton) --
    // Server-Sent Events hub for pushing real-time updates to connected clients.
    builder.Services.AddSingleton<ISseHub, InMemorySseHub>();

    // -- Application Service (Scoped) --
    // AddScoped<T>() creates one instance per HTTP request scope. This ensures
    // the service gets a fresh instance each request, which is appropriate for
    // services that depend on the scoped DbContext.
    builder.Services.AddScoped<TransactionService>();

    // -- Controllers --
    // AddControllers() registers the MVC controller services so that classes
    // annotated with [ApiController] are discovered and their routes registered.
    builder.Services.AddControllers();

    // -- OpenAPI / Swagger --
    // AddOpenApi() generates an OpenAPI (Swagger) document from your controller
    // code and data models. AddSwaggerGen provides the full Swagger UI experience.
    builder.Services.AddOpenApi();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddMetrics();

    // -- JWT Authentication --
    var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
    if (jwtSecretKey.Length < 32)
        throw new InvalidOperationException("Jwt:SecretKey must be at least 32 characters.");
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "FinancialPlatform";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtIssuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
            };
        });
    builder.Services.AddAuthorization();

    // and configuration registered above.
    var app = builder.Build();

    // Swagger UI available in all environments.
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction Service v1");
        options.RoutePrefix = "swagger";
    });

    // -- Auto-apply EF Core Migrations --
    // On startup, we check if there are pending database migrations and apply them.
    // CreateScope() creates a temporary DI scope so we can resolve scoped services
    // (like DbContext) outside of an HTTP request context.
    using (var scope = app.Services.CreateScope())
    {
        // GetRequiredService<T>() resolves a service from the DI container.
        // If the service is not registered, it throws an exception.
        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();

        // IsRelational() returns true for real databases (SQL Server, PostgreSQL, etc.)
        // and false for in-memory databases used in tests. We skip migrations for
        // in-memory databases because they don't support the concept of migrations.
        if (db.Database.IsRelational())
            // MigrateAsync() applies any pending Entity Framework migrations to the
            // database, creating or updating tables as needed. This is useful in
            // development but should be used carefully in production.
            await db.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        // MapOpenApi() maps the OpenAPI endpoint (typically /openapi/v1.json)
        // so the Swagger UI or other tools can discover the API specification.
        app.MapOpenApi();
    }

    // UseSerilogRequestLogging() adds middleware that logs each HTTP request
    // with details like method, path, status code, and elapsed time.
    app.UseSerilogRequestLogging();

    // JWT authentication and authorization middleware.
    app.UseAuthentication();
    app.UseAuthorization();

    // MapControllers() maps attribute-routed controllers (those with [Route] and
    // [HttpGet]/[HttpPost] attributes) to the request pipeline.
    app.MapControllers();
    app.MapMetrics();
    var eventBus = app.Services.GetRequiredService<IEventBus>();
    if (eventBus is AdaptiveEventBus adaptiveBus)
    {
        FinancialPlatform.TransactionService.ServiceMetrics.EventBusCurrentBackend.Set((double)adaptiveBus.CurrentBackend);
        adaptiveBus.BackendChanged += (_, backend) => FinancialPlatform.TransactionService.ServiceMetrics.EventBusCurrentBackend.Set((double)backend);
    }
    await eventBus.StartAsync();

    Log.Information("Transaction Service listening on port 5001");

    // app.Run() starts the web server and blocks the thread until the app is shut down.
    app.Run();
}
catch (Exception ex)
{
    // Log.Fatal() logs at the highest severity level, used for unrecoverable errors.
    Log.Fatal(ex, "Transaction Service terminated unexpectedly");
}
finally
{
    // CloseAndFlush() ensures any buffered log messages are written before the
    // process exits. Without this, the last few log entries may be lost.
    Log.CloseAndFlush();
}

// This partial class declaration allows integration tests to reference the
// Program class directly (via WebApplicationFactory<Program>) without needing
// a separate entry point. The "partial" keyword means the class definition
// can be split across multiple files.
public partial class Program { }
