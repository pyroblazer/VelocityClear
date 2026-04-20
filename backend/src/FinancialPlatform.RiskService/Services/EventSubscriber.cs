// ============================================================================
// EventSubscriber.cs - Background Event Listener for Risk Service
// ============================================================================
// This class subscribes to TransactionCreatedEvent messages from the event bus
// and forwards them to the RiskEvaluationService for processing. It runs as a
// background service - meaning it starts when the application starts and keeps
// running for the entire lifetime of the application.
//
// Key concepts:
//   - BackgroundService: An abstract base class for long-running asynchronous
//     operations. You override ExecuteAsync() with your background logic.
//   - ExecuteAsync(): The method that runs when the hosted service starts.
//     It should run for the lifetime of the application. When the returned
//     Task completes, the service stops.
//   - CancellationToken: A mechanism to signal that an operation should be
//     cancelled. The framework provides this token and triggers it when the
//     application is shutting down.
//   - TaskCompletionSource: A way to create a Task that you can manually
//     complete. Used here to keep the background service alive indefinitely
//     until cancellation is requested.
//   - Lambda expressions (async evt => { ... }): Anonymous async functions
//     passed as callbacks to the event bus subscription.
// ============================================================================

using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;

namespace FinancialPlatform.RiskService.Services;

// BackgroundService is an abstract class that implements IHostedService.
// It provides the boilerplate for starting and stopping a long-running task.
// You only need to override ExecuteAsync() with your background logic.
public class EventSubscriber : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly RiskEvaluationService _riskService;
    private readonly ILogger<EventSubscriber> _logger;

    public EventSubscriber(
        IEventBus eventBus,
        RiskEvaluationService riskService,
        ILogger<EventSubscriber> logger)
    {
        _eventBus = eventBus;
        _riskService = riskService;
        _logger = logger;
    }

    // ExecuteAsync() is the main method of a BackgroundService. It's called
    // once when the host starts. The returned Task should not complete until
    // the service should stop (typically when the application shuts down).
    //
    // CancellationToken stoppingToken: The framework sets this token to
    // "cancellation requested" when the application is shutting down. You
    // should check it periodically and exit gracefully when triggered.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RiskService EventSubscriber starting...");

        // SubscribeAsync<T>() registers a handler for a specific event type.
        // The generic type parameter <TransactionCreatedEvent> tells the event
        // bus which event type this handler is interested in.
        //
        // The "async evt => { ... }" syntax is an async lambda expression -
        // an inline anonymous function that can use await.
        await _eventBus.SubscribeAsync<TransactionCreatedEvent>(async evt =>
        {
            _logger.LogDebug("Received TransactionCreatedEvent for tx {TxId}", evt.TransactionId);

            try
            {
                // Forward the event to the risk evaluation service for processing.
                await _riskService.EvaluateAsync(evt);
            }
            catch (Exception ex)
            {
                // Log the error but don't rethrow - if we did, the event bus
                // subscription might break. Better to log and continue processing
                // future events.
                _logger.LogError(ex, "Error evaluating risk for tx {TxId}", evt.TransactionId);
            }
        });

        // TaskCompletionSource (without generic parameter) creates a
        // TaskCompletionSource<Task> (a "void" task). It creates a Task that
        // won't complete until we explicitly call SetResult().
        // This keeps the ExecuteAsync method alive until shutdown.
        var tcs = new TaskCompletionSource();

        // stoppingToken.Register() registers a callback that fires when the
        // cancellation token is triggered (i.e., the app is shutting down).
        // The lambda () => tcs.SetResult() completes the TaskCompletionSource,
        // which unblocks the "await tcs.Task" below.
        stoppingToken.Register(() => tcs.SetResult());

        // await tcs.Task blocks this method until the TaskCompletionSource
        // is completed (when shutdown is requested). This is the "keep alive"
        // mechanism - without it, ExecuteAsync would return immediately.
        await tcs.Task;

        _logger.LogInformation("RiskService EventSubscriber stopping...");
    }
}
