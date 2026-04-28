// ============================================================================
// Program.cs - Compliance Service Entry Point
// ============================================================================
// This is the startup file for the Compliance Service microservice. It provides
// audit logging with cryptographic hash chaining (similar to a blockchain) to
// ensure audit log integrity. It also exposes Prometheus metrics for monitoring.
//
// Key concepts:
//   - AddDbContext<T> with Scoped lifetime: Registered implicitly by
//     AddDbContext - each HTTP request gets its own DbContext instance.
//   - AddMetrics(): Registers Prometheus metrics collection for monitoring.
//   - AddHostedService<T>: Starts the EventSubscriber background service.
//   - MapMetrics(): Exposes the /metrics endpoint for Prometheus scraping.
//   - MigrateAsync() with IsRelational() check: Auto-applies database
//     migrations only for real databases, not in-memory test databases.
// ============================================================================

using System.Text;
using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.EventInfrastructure.Configuration;
using FinancialPlatform.EventInfrastructure.Sse;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;

// Bootstrap logger - captures early startup errors before full Serilog config.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()  // Adds contextual properties (like request ID) to logs
    .CreateLogger();

try
{
    Log.Information("Starting ComplianceService...");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog as the logging provider.
    builder.Host.UseSerilog();

    // -- DbContext (Scoped) --
    // AddDbContext<ComplianceDbContext> registers the database context.
    // Internally it uses Scoped lifetime - one instance per HTTP request.
    // UseSqlServer() configures SQL Server with a connection string from
    // the app's configuration (appsettings.json or environment variables).
    builder.Services.AddDbContext<ComplianceDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // -- Event Bus (Singleton) --
    builder.Services.AddSingleton<IEventBus>(sp =>
    {
        var config = new EventBusConnectionConfig(
            DefaultBackend: builder.Configuration["EventBus:DefaultBackend"] ?? "InMemory",
            RedisUrl: builder.Configuration["EventBus:RedisUrl"] ?? "localhost:6379",
            RabbitMqUrl: builder.Configuration["EventBus:RabbitMqUrl"] ?? "amqp://guest:guest@localhost:5672",
            KafkaBrokers: builder.Configuration["EventBus:KafkaBrokers"] ?? "localhost:9092",
            ServiceName: builder.Configuration["EventBus:ServiceName"] ?? "compliance-service"
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
    builder.Services.AddSingleton<ISseHub, InMemorySseHub>();

    // -- Application Services --
    builder.Services.AddScoped<AuditService>();
    builder.Services.AddScoped<KycService>();
    builder.Services.AddScoped<ConsentService>();
    builder.Services.AddScoped<AmlMonitoringService>();
    builder.Services.AddScoped<SarService>();
    builder.Services.AddScoped<ApprovalService>();
    builder.Services.AddScoped<AccessControlService>();
    builder.Services.AddScoped<ReportingService>();
    builder.Services.AddScoped<ComplaintService>();
    builder.Services.AddScoped<DigitalSignatureService>();
    builder.Services.AddScoped<SocService>();
    builder.Services.AddScoped<InfrastructureComplianceService>();
    builder.Services.AddScoped<DataMaskingService>();
    builder.Services.AddSingleton<WormStorageService>();

    // -- Prometheus Metrics --
    // AddMetrics() registers the Prometheus metrics infrastructure.
    builder.Services.AddMetrics();

    // Enable MVC controllers.
    builder.Services.AddControllers();

    // Register OpenAPI document generation.
    builder.Services.AddOpenApi();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // -- Background Event Subscriber --
    // AddHostedService<T>() registers EventSubscriber as a background service.
    // It starts automatically when the app starts and runs for its lifetime.
    builder.Services.AddHostedService<EventSubscriber>();

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

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Compliance Service v1");
        options.RoutePrefix = "swagger";
    });

    // -- Auto-apply EF Core Migrations --
    // CreateScope() creates a temporary DI scope for resolving scoped services
    // outside of an HTTP request (DbContext is scoped, so we need a scope).
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();

        // IsRelational() returns true for real databases (SQL Server, PostgreSQL)
        // and false for in-memory databases (used in tests). Migrations are only
        // meaningful for real databases - in-memory databases create tables on the fly.
        if (db.Database.IsRelational())
            // MigrateAsync() applies any pending migrations, creating or updating
            // database schema to match the current entity model definitions.
            await db.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    // Add structured HTTP request logging middleware.
    app.UseSerilogRequestLogging();

    // JWT authentication and authorization middleware.
    app.UseAuthentication();
    app.UseAuthorization();

    // Map attribute-routed controllers.
    app.MapControllers();

    // -- Prometheus Metrics Endpoint --
    // MapMetrics() exposes the /metrics endpoint that Prometheus scrapes to
    // collect application metrics (counters, gauges, histograms, etc.).
    app.MapMetrics();

    var eventBus = app.Services.GetRequiredService<IEventBus>();
    if (eventBus is AdaptiveEventBus adaptiveBus)
    {
        FinancialPlatform.ComplianceService.ServiceMetrics.EventBusCurrentBackend.Set((double)adaptiveBus.CurrentBackend);
        adaptiveBus.BackendChanged += (_, backend) => FinancialPlatform.ComplianceService.ServiceMetrics.EventBusCurrentBackend.Set((double)backend);
    }

    app.Run("http://0.0.0.0:5004");
}
catch (Exception ex)
{
    Log.Fatal(ex, "ComplianceService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
