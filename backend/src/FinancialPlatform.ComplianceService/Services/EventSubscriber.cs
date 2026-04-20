// ============================================================================
// EventSubscriber.cs - Background Event Listener for Compliance Service
// ============================================================================
// This background service subscribes to all event types in the platform
// (TransactionCreatedEvent, RiskEvaluatedEvent, PaymentAuthorizedEvent) and
// forwards each to the AuditService for logging. It demonstrates an important
// DI pattern: since the AuditService is Scoped (it depends on a Scoped
// DbContext) but this BackgroundService is a Singleton, we must use
// IServiceScopeFactory to create a new scope for each event we handle.
//
// Key concepts:
//   - BackgroundService: Base class for long-running background operations.
//   - IServiceScopeFactory: A Singleton service that can create new DI scopes.
//     This is the bridge between Singleton and Scoped services - the factory
//     itself is Singleton, but each scope it creates has its own Scoped instances.
//   - nameof(): Returns the name of a type/member as a string - refactoring-safe.
//   - ExecuteAsync(): The method that runs for the lifetime of the application.
// ============================================================================

using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;

namespace FinancialPlatform.ComplianceService.Services;

// Inherits from BackgroundService, which provides the IHostedService lifecycle.
// BackgroundService runs ExecuteAsync() in the background for the app's lifetime.
public class EventSubscriber : BackgroundService
{
    private readonly IEventBus _eventBus;

    // IServiceScopeFactory is injected here because EventSubscriber is registered
    // as a Singleton (via AddHostedService), but AuditService is Scoped (it depends
    // on ComplianceDbContext which is Scoped). A Singleton cannot directly depend on
    // a Scoped service - instead, it uses IServiceScopeFactory to create a temporary
    // scope and resolve the Scoped service within that scope.
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventSubscriber> _logger;

    public EventSubscriber(
        IEventBus eventBus,
        IServiceScopeFactory scopeFactory,
        ILogger<EventSubscriber> logger)
    {
        _eventBus = eventBus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ExecuteAsync() is called once by the framework when the host starts.
    // It should remain running (not complete) until the application shuts down.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventSubscriber starting, registering handlers...");

        // Subscribe to TransactionCreatedEvent - fired when a new transaction is created.
        // nameof(TransactionCreatedEvent) returns "TransactionCreatedEvent" as a string.
        // This is safer than writing the string literal because renaming the class
        // would cause a compiler error instead of a silent bug.
        await _eventBus.SubscribeAsync<TransactionCreatedEvent>(async evt =>
        {
            await HandleEventAsync(nameof(TransactionCreatedEvent), evt);
        });

        // Subscribe to RiskEvaluatedEvent - fired when risk scoring is complete.
        await _eventBus.SubscribeAsync<RiskEvaluatedEvent>(async evt =>
        {
            await HandleEventAsync(nameof(RiskEvaluatedEvent), evt);
        });

        // Subscribe to PaymentAuthorizedEvent - fired when payment is authorized/rejected.
        await _eventBus.SubscribeAsync<PaymentAuthorizedEvent>(async evt =>
        {
            await HandleEventAsync(nameof(PaymentAuthorizedEvent), evt);
        });

        // Subscribe to PinVerifiedEvent - fired when PIN verification completes.
        await _eventBus.SubscribeAsync<PinVerifiedEvent>(async evt =>
        {
            await HandleEventAsync(nameof(PinVerifiedEvent), evt);
        });

        // StartAsync() initializes the event bus so it begins delivering events
        // to the registered handlers above.
        await _eventBus.StartAsync(stoppingToken);

        _logger.LogInformation("EventSubscriber started and subscribed to all events");

        // Keep alive until cancellation is requested (app shutdown).
        // TaskCompletionSource creates a manually-controlled Task.
        var tcs = new TaskCompletionSource();

        // TrySetResult() (instead of SetResult()) is safer - it won't throw if
        // the TCS has already been completed (e.g., if cancellation was already
        // requested when we reach this point).
        stoppingToken.Register(() => tcs.TrySetResult());

        // This await keeps ExecuteAsync running until shutdown.
        await tcs.Task;

        _logger.LogInformation("EventSubscriber stopping...");

        // Gracefully stop the event bus when shutting down.
        await _eventBus.StopAsync(stoppingToken);
    }

    // This method handles each event by creating a new DI scope and resolving
    // the AuditService within it. This is the standard pattern for consuming
    // Scoped services from a Singleton BackgroundService.
    private async Task HandleEventAsync(string eventType, object payload)
    {
        try
        {
            // Create a new DI scope. "using var" ensures the scope is disposed
            // (and the DbContext within it is cleaned up) when the method exits.
            // This is the C# "using declaration" syntax - no braces needed.
            using var scope = _scopeFactory.CreateScope();

            // Resolve AuditService from the new scope. This gives us a fresh
            // AuditService with its own ComplianceDbContext - not shared with
            // any other scope or concurrent event handler.
            var auditService = scope.ServiceProvider.GetRequiredService<AuditService>();

            await auditService.LogEventAsync(eventType, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to audit event {EventType}", eventType);
        }
    }
}
