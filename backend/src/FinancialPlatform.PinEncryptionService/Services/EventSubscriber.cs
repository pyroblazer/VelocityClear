// ============================================================================
// EventSubscriber.cs - Background Event Listener for PIN Encryption Service
// ============================================================================
// This background service subscribes to TransactionCreatedEvent and performs
// PIN verification for card transactions (those carrying a PAN and PIN block).
// It publishes PinVerifiedEvent which flows into the payment authorization
// pipeline alongside RiskEvaluatedEvent.
//
// Event flow for card transactions:
//   TransactionCreated ──► PinVerified ──┐
//           │                            ├─► PaymentAuthorized ──► AuditLogged
//           └──► RiskEvaluated ──────────┘
//
// Non-card transactions (without PAN/PIN block) are silently skipped.
//
// Key concepts:
//   - BackgroundService: A .NET base class for long-running background tasks
//     that start with the application and run until shutdown.
//   - IEventBus: A publish/subscribe messaging interface. SubscribeAsync
//     registers a handler; PublishAsync sends an event to all subscribers.
//   - CancellationToken: A signal from the framework that the application is
//     shutting down. Used to gracefully stop the event bus.
// ============================================================================

using FinancialPlatform.PinEncryptionService.Models;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;

namespace FinancialPlatform.PinEncryptionService.Services;

public class EventSubscriber : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly IHsmService _hsmService;
    private readonly ILogger<EventSubscriber> _logger;

    public EventSubscriber(
        IEventBus eventBus,
        IHsmService hsmService,
        ILogger<EventSubscriber> logger)
    {
        _eventBus = eventBus;
        _hsmService = hsmService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PinEncryptionService EventSubscriber starting...");

        // Subscribe to TransactionCreatedEvent to verify PINs on card transactions.
        // When a transaction contains a PAN and PIN block, this handler decrypts
        // the PIN via the HSM and publishes a PinVerifiedEvent with the result.
        await _eventBus.SubscribeAsync<TransactionCreatedEvent>(async evt =>
        {
            // Non-card transactions (e.g., wire transfers) have no PAN or PIN block.
            if (string.IsNullOrEmpty(evt.Pan) || string.IsNullOrEmpty(evt.PinBlock))
            {
                _logger.LogDebug("Skipping non-card transaction {TxId}", evt.TransactionId);
                return;
            }

            _logger.LogInformation("Processing card transaction {TxId} for PAN ending {Suffix}",
                evt.TransactionId, evt.Pan[^4..]);

            try
            {
                // Select the first available Zone PIN Key (ZPK) from the HSM key store.
                // ZPK is a symmetric key used to encrypt PIN blocks during transport.
                var zpkId = _hsmService.ListKeyIds().FirstOrDefault(k => true) ?? "default-zpk";
                var decrypted = _hsmService.DecryptPin(
                    new DecryptPinRequest(evt.PinBlock, evt.Pan, zpkId));

                // A valid PIN must be 4-12 numeric digits (ISO 9564 standard).
                var isValidPin = !string.IsNullOrEmpty(decrypted.Pin) &&
                    decrypted.Pin.Length >= 4 && decrypted.Pin.All(char.IsDigit);

                await _eventBus.PublishAsync(new PinVerifiedEvent(
                    evt.TransactionId,
                    evt.Pan,
                    isValidPin,
                    isValidPin ? "PIN block decrypted successfully" : "Invalid PIN block format",
                    DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PIN verification failed for tx {TxId}", evt.TransactionId);
                await _eventBus.PublishAsync(new PinVerifiedEvent(
                    evt.TransactionId,
                    evt.Pan,
                    false,
                    $"HSM error: {ex.Message}",
                    DateTime.UtcNow));
            }
        });

        // Initialize the event bus so it begins delivering events to handlers.
        await _eventBus.StartAsync(stoppingToken);

        _logger.LogInformation("PinEncryptionService EventSubscriber started and subscribed to TransactionCreatedEvent");

        // Keep the background service alive until the application shuts down.
        var tcs = new TaskCompletionSource();
        stoppingToken.Register(() => tcs.TrySetResult());
        await tcs.Task;

        _logger.LogInformation("PinEncryptionService EventSubscriber stopping...");

        // Gracefully shut down the event bus (closes connections, releases resources).
        await _eventBus.StopAsync(stoppingToken);
    }
}
