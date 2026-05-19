using System.Reflection;
using System.Text;
using FinancialPlatform.AllServices.Services;
using FinancialPlatform.ApiGateway.Data;
using FinancialPlatform.ApiGateway.Middleware;
using FinancialPlatform.ApiGateway.Services;
using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.EventInfrastructure.Configuration;
using FinancialPlatform.EventInfrastructure.Sse;
using FinancialPlatform.PaymentService.Services;
using FinancialPlatform.PinEncryptionService.Services;
using FinancialPlatform.RiskService.Services;
using FinancialPlatform.Shared.Interfaces;
using FinancialPlatform.TransactionService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;
using ApiKeyMiddleware = FinancialPlatform.ApiGateway.Middleware.ApiKeyMiddleware;
using RateLimitingMiddleware = FinancialPlatform.ApiGateway.Middleware.RateLimitingMiddleware;
using RateLimitingOptions = FinancialPlatform.ApiGateway.Middleware.RateLimitingOptions;
using RequestLoggingMiddleware = FinancialPlatform.ApiGateway.Middleware.RequestLoggingMiddleware;
using RiskEventSubscriber = FinancialPlatform.RiskService.Services.EventSubscriber;
using PaymentEventSubscriber = FinancialPlatform.PaymentService.Services.EventSubscriber;
using PinEventSubscriber = FinancialPlatform.PinEncryptionService.Services.EventSubscriber;
using ComplianceEventSubscriber = FinancialPlatform.ComplianceService.Services.EventSubscriber;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting VelocityClear Platform (AllServices)");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, config) =>
    {
        config.ReadFrom.Configuration(context.Configuration);
    });

    // ── DbContexts ───────────────────────────────────────────────────────
    // All three point to the same MonsterASP.net database; EF Core uses
    // different table names per entity so there are no collisions.
    builder.Services.AddDbContext<GatewayDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddDbContext<TransactionDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddDbContext<ComplianceDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // ── Event Bus (single shared instance) ───────────────────────────────
    builder.Services.AddSingleton<IEventBus>(sp =>
    {
        var config = new EventBusConnectionConfig(
            DefaultBackend: builder.Configuration["EventBus:DefaultBackend"] ?? "InMemory",
            RedisUrl: builder.Configuration["EventBus:RedisUrl"] ?? "localhost:6379",
            RabbitMqUrl: builder.Configuration["EventBus:RabbitMqUrl"] ?? "amqp://guest:guest@localhost:5672",
            KafkaBrokers: builder.Configuration["EventBus:KafkaBrokers"] ?? "localhost:9092",
            ServiceName: builder.Configuration["EventBus:ServiceName"] ?? "velocityclear-platform"
        );
        return new AdaptiveEventBus(
            config,
            sp.GetRequiredService<ILogger<AdaptiveEventBus>>(),
            sp.GetRequiredService<ILogger<InMemoryEventBus>>(),
            sp.GetRequiredService<ILogger<RedisEventBus>>(),
            sp.GetRequiredService<ILogger<RabbitMQEventBus>>(),
            sp.GetRequiredService<ILogger<KafkaEventBus>>());
    });

    // ── SSE Hub ──────────────────────────────────────────────────────────
    builder.Services.AddSingleton<ISseHub, InMemorySseHub>();

    // ── API Gateway services ─────────────────────────────────────────────
    builder.Services.AddSingleton<JwtService>();
    builder.Services.AddHttpClient();
    builder.Services.Configure<RateLimitingOptions>(
        builder.Configuration.GetSection("RateLimiting"));

    // ── Transaction Service ──────────────────────────────────────────────
    builder.Services.AddScoped<FinancialPlatform.TransactionService.Services.TransactionService>();

    // ── Risk Service ─────────────────────────────────────────────────────
    builder.Services.AddSingleton<AmlRuleEngine>();
    builder.Services.AddSingleton<RiskEvaluationService>();

    // ── Payment Service ──────────────────────────────────────────────────
    builder.Services.AddSingleton<PaymentGateway>();
    builder.Services.AddSingleton<FinancialPlatform.PaymentService.Services.PaymentService>();

    // ── Compliance Service ───────────────────────────────────────────────
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

    // ── PIN Encryption Service ───────────────────────────────────────────
    builder.Services.AddSingleton<PinBlockService>();
    builder.Services.AddSingleton<IHsmService, SoftwareHsmService>();
    builder.Services.AddSingleton<Iso8583Service>();

    // ── Background Services (event subscribers) ──────────────────────────
    builder.Services.AddHostedService<RiskEventSubscriber>();
    builder.Services.AddHostedService<PaymentEventSubscriber>();
    builder.Services.AddHostedService<PinEventSubscriber>();
    builder.Services.AddHostedService<ComplianceEventSubscriber>();
    builder.Services.AddHostedService<KafkaKeepAliveService>();

    // ── Controllers from all assemblies ──────────────────────────────────
    builder.Services.AddControllers()
        .AddApplicationPart(Assembly.Load("FinancialPlatform.ApiGateway"))
        .AddApplicationPart(Assembly.Load("FinancialPlatform.TransactionService"))
        .AddApplicationPart(Assembly.Load("FinancialPlatform.RiskService"))
        .AddApplicationPart(Assembly.Load("FinancialPlatform.PaymentService"))
        .AddApplicationPart(Assembly.Load("FinancialPlatform.ComplianceService"))
        .AddApplicationPart(Assembly.Load("FinancialPlatform.PinEncryptionService"));

    // ── Swagger ──────────────────────────────────────────────────────────
    builder.Services.AddOpenApi();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // ── Prometheus ───────────────────────────────────────────────────────
    builder.Services.AddMetrics();

    // ── JWT Authentication ───────────────────────────────────────────────
    var secretKey = builder.Configuration["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
    if (secretKey.Length < 32)
        throw new InvalidOperationException("Jwt:SecretKey must be at least 32 characters.");
    var issuer = builder.Configuration["Jwt:Issuer"] ?? "FinancialPlatform";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = issuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
            };
        });
    builder.Services.AddAuthorization();

    // ── Build ────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Auto-migrate all databases ───────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var migrateLog = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            await MigrateIfRelationalAsync<GatewayDbContext>(scope);
            await MigrateIfRelationalAsync<TransactionDbContext>(scope);
            await MigrateIfRelationalAsync<ComplianceDbContext>(scope);
            migrateLog.LogInformation("All databases migrated successfully.");
        }
        catch (Exception ex)
        {
            migrateLog.LogError(ex, "Database migration failed. The app will continue but database operations may fail.");
        }
    }

    // ── Middleware pipeline ──────────────────────────────────────────────
    app.UseMiddleware<ApiKeyMiddleware>();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "VelocityClear Platform v1");
        options.RoutePrefix = "swagger";
    });
    app.UseMiddleware<RateLimitingMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseSerilogRequestLogging();
    app.MapControllers();
    app.MapMetrics();

    // ── Wire up Prometheus metrics for event bus backend tracking ────────
    var eventBus = app.Services.GetRequiredService<IEventBus>();
    if (eventBus is AdaptiveEventBus adaptiveBus)
    {
        var backendGauge = Metrics.CreateGauge(
            "eventbus_current_backend",
            "Current event bus backend: 0=InMemory, 1=Redis, 2=RabbitMQ, 3=Kafka");
        backendGauge.Set((double)adaptiveBus.CurrentBackend);
        adaptiveBus.BackendChanged += (_, backend) => backendGauge.Set((double)backend);
    }

    Log.Information("VelocityClear Platform starting. Hosting environment: {Env}", app.Environment.EnvironmentName);
    // Don't hardcode the URL — IIS InProcess hosting sets the port via ASPNETCORE_URLS.
    // For Docker/standalone, pass --urls http://0.0.0.0:5000 or set ASPNETCORE_URLS.
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "VelocityClear Platform terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static async Task MigrateIfRelationalAsync<TDbContext>(IServiceScope scope)
    where TDbContext : DbContext
{
    var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
}

public partial class Program { }
