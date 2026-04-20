// ============================================================================
// EventSubscriber.cs - Background Event Listener for Payment Service
// ============================================================================
// This background service subscribes to two event types from the event bus:
//   1. TransactionCreatedEvent - captures the transaction amount for later use.
//   2. RiskEvaluatedEvent - triggers the actual payment authorization logic.
//
// The payment flow is a two-step process across two separate events because
// the risk score (from RiskEvaluatedEvent) and the transaction amount (from
// TransactionCreatedEvent) arrive in separate messages.
//
// Key concepts:
//   - BackgroundService: Base class for services that run for the app's lifetime.
//   - Multiple subscriptions: One service can subscribe to multiple event types.
//   - Task.CompletedTask: A pre-completed Task - used when a method signature
//     requires returning a Task but no async work is needed.
//   - TaskCompletionSource: Used to keep the background service alive until
//     the application shuts down (cancellation is requested).
// ============================================================================

using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;

namespace FinancialPlatform.PaymentService.Services;

// Inherits from BackgroundService, which implements IHostedService and manages
// the lifecycle of a long-running background task.
public class EventSubscriber : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly PaymentService _paymentService;
    private readonly ILogger<EventSubscriber> _logger;

    public EventSubscriber(
        IEventBus eventBus,
        PaymentService paymentService,
        ILogger<EventSubscriber> logger)
    {
        _eventBus = eventBus;
        _paymentService = paymentService;
        _logger = logger;
    }

    // ExecuteAsync() is called once when the host starts. It must remain running
    // for the lifetime of the application. The CancellationToken is triggered by
    // the framework when the application is shutting down.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentService EventSubscriber starting...");

        // -- Subscription 1: TransactionCreatedEvent --
        // When a new transaction is created, we register its amount in memory.
        // This amount will be needed later when the risk evaluation arrives.
        await _eventBus.SubscribeAsync<TransactionCreatedEvent>(async evt =>
        {
            _logger.LogDebug("Received TransactionCreatedEvent for tx {TxId}", evt.TransactionId);

            // RegisterTransaction stores the amount for later lookup.
            _paymentService.RegisterTransaction(evt.TransactionId, evt.Amount);

            // Task.CompletedTask is a cached, already-completed Task. It's used
            // here because the lambda is declared "async" (required by the
            // SubscribeAsync signature) but doesn't actually perform any async work.
            await Task.CompletedTask;
        });

        // -- Subscription 2: RiskEvaluatedEvent --
        // When the risk evaluation is complete, process the payment using the
        // previously stored amount and the new risk score.
        await _eventBus.SubscribeAsync<RiskEvaluatedEvent>(async evt =>
        {
            _logger.LogDebug("Received RiskEvaluatedEvent for tx {TxId}", evt.TransactionId);

            try
            {
                await _paymentService.ProcessPaymentAsync(evt);
            }
            catch (Exception ex)
            {
                // Log the error but don't rethrow - we want to continue processing
                // future events even if one fails.
                _logger.LogError(ex, "Error processing payment for tx {TxId}", evt.TransactionId);
            }
        });

        // -- Subscription 3: PinVerifiedEvent --
        // When PIN verification completes, record the result for use during
        // payment authorization.
        await _eventBus.SubscribeAsync<PinVerifiedEvent>(async evt =>
        {
            _logger.LogDebug("Received PinVerifiedEvent for tx {TxId}", evt.TransactionId);
            _paymentService.RecordPinVerification(evt.TransactionId, evt.Verified);
            await Task.CompletedTask;
        });

        // Keep the subscriber alive until cancellation is requested.
        // TaskCompletionSource creates a Task we manually control. It won't
        // complete until we call SetResult(), which happens when the
        // stoppingToken is cancelled (i.e., the app is shutting down).
        var tcs = new TaskCompletionSource();

        // Register a callback on the cancellation token that completes the TCS
        // when the application is shutting down.
        stoppingToken.Register(() => tcs.SetResult());

        // This await blocks ExecuteAsync until shutdown, keeping the background
        // service alive and the subscriptions active.
        await tcs.Task;

        _logger.LogInformation("PaymentService EventSubscriber stopping...");
    }
}
