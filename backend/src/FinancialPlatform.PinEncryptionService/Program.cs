// ============================================================================
// Program.cs - PIN Encryption Service Application Entry Point
// ============================================================================
// This is the startup file for the PIN Encryption microservice. It configures
// all dependencies and starts the web server on port 5005.
//
// This service provides:
//   1. HSM (Hardware Security Module) simulation - cryptographic key management
//      and PIN encryption/decryption/verification operations.
//   2. ISO 8583 message processing - parse, build, and authorize card transactions
//      using the international financial messaging standard.
//   3. Event-driven PIN verification - subscribes to TransactionCreatedEvent,
//      verifies PIN blocks for card transactions, and publishes PinVerifiedEvent.
//
// In the platform architecture, this service sits alongside the core pipeline:
//   TransactionCreated ──► PinVerified ──┐
//           │                            ├─► PaymentAuthorized ──► AuditLogged
//           └──► RiskEvaluated ──────────┘
//
// Key C# concepts:
//   Top-level statements - No explicit Main() method; the compiler generates one.
//   builder.Services - The dependency injection (DI) container where services
//     are registered. Controllers and other classes receive these via constructors.
//   AddSingleton<T>() - Creates one instance shared across the entire app lifetime.
//   AddHostedService<T>() - Registers a BackgroundService that runs for the app's lifetime.
// ============================================================================

using System.Text;
using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.EventInfrastructure.Configuration;
using FinancialPlatform.EventInfrastructure.Sse;
using FinancialPlatform.PinEncryptionService.Services;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;

// Set up a console-only logger before the host is built so startup errors
// are captured. CreateLogger() (not CreateBootstrapLogger()) avoids the
// ReloadableLogger freeze issue when multiple WebApplicationFactory instances
// run in the same test process.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog as the logging provider, reading settings from configuration.
    builder.Host.UseSerilog((context, config) =>
        config.ReadFrom.Configuration(context.Configuration));

    // Register the adaptive event bus for publish/subscribe messaging.
    // This service subscribes to TransactionCreatedEvent and publishes PinVerifiedEvent.
    builder.Services.AddSingleton<IEventBus>(sp =>
    {
        var config = new EventBusConnectionConfig(
            DefaultBackend: builder.Configuration["EventBus:DefaultBackend"] ?? "InMemory",
            RedisUrl: builder.Configuration["EventBus:RedisUrl"] ?? "localhost:6379",
            RabbitMqUrl: builder.Configuration["EventBus:RabbitMqUrl"] ?? "amqp://guest:guest@localhost:5672",
            KafkaBrokers: builder.Configuration["EventBus:KafkaBrokers"] ?? "localhost:9092",
            ServiceName: builder.Configuration["EventBus:ServiceName"] ?? "pin-encryption-service"
        );
        return new AdaptiveEventBus(
            config,
            sp.GetRequiredService<ILogger<AdaptiveEventBus>>(),
            sp.GetRequiredService<ILogger<InMemoryEventBus>>(),
            sp.GetRequiredService<ILogger<RedisEventBus>>(),
            sp.GetRequiredService<ILogger<RabbitMQEventBus>>(),
            sp.GetRequiredService<ILogger<KafkaEventBus>>());
    });

    // SSE hub for broadcasting events to connected frontends in real time.
    builder.Services.AddSingleton<ISseHub, InMemorySseHub>();

    // Register the core cryptographic services as singletons (shared across all requests).
    builder.Services.AddSingleton<PinBlockService>();                   // ISO 9564 PIN block operations
    builder.Services.AddSingleton<IHsmService, SoftwareHsmService>();   // Simulated HSM key store
    builder.Services.AddSingleton<Iso8583Service>();                    // ISO 8583 message parser/builder

    // Register the background service that subscribes to events from the bus.
    // It listens for TransactionCreatedEvent and verifies PINs on card transactions.
    builder.Services.AddHostedService<EventSubscriber>();

    builder.Services.AddControllers();
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

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PIN Encryption Service v1");
        options.RoutePrefix = "swagger";
    });

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapMetrics();

    var eventBus = app.Services.GetRequiredService<IEventBus>();
    if (eventBus is AdaptiveEventBus adaptiveBus)
    {
        FinancialPlatform.PinEncryptionService.ServiceMetrics.EventBusCurrentBackend.Set((double)adaptiveBus.CurrentBackend);
        adaptiveBus.BackendChanged += (_, backend) => FinancialPlatform.PinEncryptionService.ServiceMetrics.EventBusCurrentBackend.Set((double)backend);
    }

    app.Run("http://0.0.0.0:5005");
}
catch (Exception ex)
{
    Log.Fatal(ex, "PinEncryptionService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Exposes the compiler-generated Program class so WebApplicationFactory<TestEntry>
// can locate this assembly's entry point in integration tests.
public partial class Program { }
