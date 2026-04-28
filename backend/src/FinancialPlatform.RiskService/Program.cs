// ============================================================================
// Program.cs - Risk Service Entry Point
// ============================================================================
// This is the startup file for the Risk Service microservice. It evaluates the
// risk level of financial transactions in real-time. Unlike the Transaction
// Service, this service has no database - it operates purely on in-memory state
// and publishes risk evaluation results to the event bus.
//
// Key concepts:
//   - AddSingleton vs AddScoped: This service uses Singleton for everything
//     because it has no database context. The risk service keeps in-memory
//     velocity tracking state that must persist across all requests.
//   - AddHostedService<T>: Registers a BackgroundService that starts when the
//     host starts and runs for the lifetime of the application. Used here to
//     subscribe to events from the event bus continuously.
// ============================================================================

using System.Text;
using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.EventInfrastructure.Configuration;
using FinancialPlatform.EventInfrastructure.Sse;
using FinancialPlatform.RiskService.Services;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;

// Bootstrap logger for capturing startup errors before full configuration is loaded.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    // Create the web application builder - this sets up configuration,
    // environment variables, logging, and the DI container.
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog as the logging provider, reading settings from
    // appsettings.json (e.g., minimum log level, output sinks).
    builder.Host.UseSerilog((context, config) =>
    {
        config.ReadFrom.Configuration(context.Configuration);
    });

    // -- Event Bus (Singleton) --
    // One shared AdaptiveEventBus instance for the entire application lifetime.
    builder.Services.AddSingleton<IEventBus>(sp =>
    {
        var config = new EventBusConnectionConfig(
            DefaultBackend: builder.Configuration["EventBus:DefaultBackend"] ?? "InMemory",
            RedisUrl: builder.Configuration["EventBus:RedisUrl"] ?? "localhost:6379",
            RabbitMqUrl: builder.Configuration["EventBus:RabbitMqUrl"] ?? "amqp://guest:guest@localhost:5672",
            KafkaBrokers: builder.Configuration["EventBus:KafkaBrokers"] ?? "localhost:9092",
            ServiceName: builder.Configuration["EventBus:ServiceName"] ?? "risk-service"
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
    // One shared SSE hub instance for broadcasting events to connected clients.
    builder.Services.AddSingleton<ISseHub, InMemorySseHub>();

    // -- Risk Evaluation Service (Singleton) --
    // Registered as Singleton because it holds in-memory state (the velocity
    // tracker dictionary) that must persist across all incoming events. If it
    // were Scoped, each HTTP request would get a new instance and lose history.
    builder.Services.AddSingleton<AmlRuleEngine>();
    builder.Services.AddSingleton<RiskEvaluationService>();

    // AddControllers() enables the MVC controller infrastructure.
    builder.Services.AddControllers();

    // -- Background Service --
    // AddHostedService<T>() registers a service that starts automatically when
    // the web host starts and runs in the background. EventSubscriber is a
    // BackgroundService that listens for TransactionCreatedEvent messages on
    // the event bus and forwards them to the RiskEvaluationService.
    builder.Services.AddHostedService<EventSubscriber>();

    // Register OpenAPI/Swagger document generation.
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
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Risk Service v1");
        options.RoutePrefix = "swagger";
    });

    if (app.Environment.IsDevelopment())
    {
        // Map the OpenAPI specification endpoint for development.
        app.MapOpenApi();
    }

    // Add request logging middleware.
    app.UseSerilogRequestLogging();

    // JWT authentication and authorization middleware.
    app.UseAuthentication();
    app.UseAuthorization();

    // Map all controller routes to the request pipeline.
    app.MapControllers();
    app.MapMetrics();

    var eventBus = app.Services.GetRequiredService<IEventBus>();
    if (eventBus is AdaptiveEventBus adaptiveBus)
    {
        FinancialPlatform.RiskService.ServiceMetrics.EventBusCurrentBackend.Set((double)adaptiveBus.CurrentBackend);
        adaptiveBus.BackendChanged += (_, backend) => FinancialPlatform.RiskService.ServiceMetrics.EventBusCurrentBackend.Set((double)backend);
    }

    app.Run("http://0.0.0.0:5002");
}
catch (Exception ex)
{
    Log.Fatal(ex, "RiskService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
