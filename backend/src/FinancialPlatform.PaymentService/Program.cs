// ============================================================================
// Program.cs - Payment Service Entry Point
// ============================================================================
// This is the startup file for the Payment Service microservice. It handles
// payment authorization based on risk scores received from the Risk Service.
// Like the Risk Service, it has no database - it uses in-memory state to track
// pending transaction amounts.
//
// Key concepts:
//   - AddSingleton: All services are registered as singletons because there is
//     no database context. The PaymentGateway and PaymentService hold shared
//     in-memory state that must be consistent across all requests.
//   - AddHostedService<EventSubscriber>: Starts a background listener that
//     processes incoming events (TransactionCreatedEvent and RiskEvaluatedEvent).
// ============================================================================

using System.Text;
using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.EventInfrastructure.Configuration;
using FinancialPlatform.EventInfrastructure.Sse;
using FinancialPlatform.PaymentService.Services;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;

// Bootstrap logger for early error capture before full configuration is available.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog for structured logging.
    builder.Host.UseSerilog((context, config) =>
    {
        config.ReadFrom.Configuration(context.Configuration);
    });

    // -- Event Bus (Singleton) --
    // Shared event bus instance for publishing and subscribing to events.
    builder.Services.AddSingleton<IEventBus>(sp =>
    {
        var config = new EventBusConnectionConfig(
            DefaultBackend: builder.Configuration["EventBus:DefaultBackend"] ?? "InMemory",
            RedisUrl: builder.Configuration["EventBus:RedisUrl"] ?? "localhost:6379",
            RabbitMqUrl: builder.Configuration["EventBus:RabbitMqUrl"] ?? "amqp://guest:guest@localhost:5672",
            KafkaBrokers: builder.Configuration["EventBus:KafkaBrokers"] ?? "localhost:9092",
            ServiceName: builder.Configuration["EventBus:ServiceName"] ?? "payment-service"
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
    // Shared hub for pushing real-time Server-Sent Events to frontend clients.
    builder.Services.AddSingleton<ISseHub, InMemorySseHub>();

    // -- Payment Services (Singleton) --
    // PaymentGateway is the authorization rules engine - holds no mutable state
    // beyond logging, so singleton is safe.
    builder.Services.AddSingleton<PaymentGateway>();

    // PaymentService tracks pending transaction amounts in memory.
    // Singleton ensures all requests share the same in-memory tracking dictionary.
    builder.Services.AddSingleton<PaymentService>();

    // Enable MVC controllers for HTTP endpoint handling.
    builder.Services.AddControllers();

    // Register the background event subscriber that listens for events and
    // dispatches them to the appropriate service methods.
    builder.Services.AddHostedService<EventSubscriber>();

    // Register OpenAPI document generation for API documentation.
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
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Service v1");
        options.RoutePrefix = "swagger";
    });

    if (app.Environment.IsDevelopment())
    {
        // Expose the OpenAPI spec endpoint in development mode.
        app.MapOpenApi();
    }

    // Add request logging middleware for structured HTTP request logging.
    app.UseSerilogRequestLogging();

    // JWT authentication and authorization middleware.
    app.UseAuthentication();
    app.UseAuthorization();

    // Map attribute-routed controllers to the middleware pipeline.
    app.MapControllers();
    app.MapMetrics();

    var eventBus = app.Services.GetRequiredService<IEventBus>();
    if (eventBus is AdaptiveEventBus adaptiveBus)
    {
        FinancialPlatform.PaymentService.ServiceMetrics.EventBusCurrentBackend.Set((double)adaptiveBus.CurrentBackend);
        adaptiveBus.BackendChanged += (_, backend) => FinancialPlatform.PaymentService.ServiceMetrics.EventBusCurrentBackend.Set((double)backend);
    }

    app.Run("http://0.0.0.0:5003");
}
catch (Exception ex)
{
    Log.Fatal(ex, "PaymentService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
