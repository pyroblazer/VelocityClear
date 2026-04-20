// ============================================================================
// PaymentService.cs - Payment Processing Coordinator
// ============================================================================
// This service coordinates the payment processing flow. It receives two types
// of events in sequence:
//   1. TransactionCreatedEvent - stores the transaction amount for later lookup.
//   2. RiskEvaluatedEvent - uses the stored amount plus the risk score to make
//      an authorization decision and publishes the result.
//
// Key concepts:
//   - Dictionary<string, decimal>: Maps transaction IDs to their amounts.
//     Used as a temporary store between the two event types.
//   - lock statement: Ensures thread-safe access to the shared dictionary.
//     Multiple events may arrive concurrently on different threads.
//   - Tuple deconstruction: "var (authorized, reason) = _gateway.Authorize(...)"
//     unpacks a method's tuple return into named variables.
//   - out parameter: "TryGetValue(key, out amount)" returns true/false and
//     sets "amount" to the found value (or default if not found).
// ============================================================================

using FinancialPlatform.PaymentService;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;

namespace FinancialPlatform.PaymentService.Services;

public class PaymentService
{
    private readonly IEventBus _eventBus;
    private readonly PaymentGateway _gateway;
    private readonly ILogger<PaymentService> _logger;

    // Dictionary<string, decimal> maps transaction IDs (strings) to their
    // amounts (decimals). This acts as a temporary cache between events:
    //   - TransactionCreatedEvent provides the amount -> we store it here.
    //   - RiskEvaluatedEvent triggers authorization -> we look up the amount.
    private readonly Dictionary<string, decimal> _pendingTransactions = new();

    private readonly Dictionary<string, bool> _pinVerificationResults = new();

    // Lock object for thread-safe access to the _pendingTransactions dictionary.
    // Since events arrive asynchronously on potentially different threads,
    // we need to synchronize access to prevent data corruption.
    private readonly object _pendingLock = new();

    private readonly object _pinLock = new();

    public PaymentService(IEventBus eventBus, PaymentGateway gateway, ILogger<PaymentService> logger)
    {
        _eventBus = eventBus;
        _gateway = gateway;
        _logger = logger;
    }

    /// <summary>
    /// Register a transaction's amount so it can be used later for payment authorization.
    /// Called when a TransactionCreatedEvent is observed.
    /// </summary>
    public void RegisterTransaction(string transactionId, decimal amount)
    {
        // lock ensures only one thread accesses _pendingTransactions at a time.
        // Without this, two threads could try to modify the dictionary
        // simultaneously, causing undefined behavior.
        lock (_pendingLock)
        {
            _pendingTransactions[transactionId] = amount;
        }
        _logger.LogDebug("Registered transaction {TxId} with amount {Amount}", transactionId, amount);
    }

    public void RecordPinVerification(string transactionId, bool verified)
    {
        lock (_pinLock)
        {
            _pinVerificationResults[transactionId] = verified;
        }
        _logger.LogDebug("Recorded PIN verification for tx {TxId}: {Verified}", transactionId, verified);
    }

    public async Task ProcessPaymentAsync(RiskEvaluatedEvent evt)
    {
        decimal amount;

        // Lock to safely read (and remove) from the shared dictionary.
        lock (_pendingLock)
        {
            // TryGetValue() is the safe way to access a dictionary:
            //   - Returns true if the key exists, false otherwise.
            //   - "out amount" receives the value if found, or default (0m) if not.
            // This avoids the KeyNotFoundException that _pendingTransactions[key]
            // would throw if the key doesn't exist.
            if (!_pendingTransactions.TryGetValue(evt.TransactionId, out amount))
            {
                _logger.LogWarning(
                    "No registered amount found for tx {TxId}, using 0 for authorization check",
                    evt.TransactionId);
                amount = 0m;
            }
            else
            {
                // Clean up: remove the entry after retrieving it so memory doesn't
                // grow indefinitely. Each transaction is only needed once.
                _pendingTransactions.Remove(evt.TransactionId);
            }
        }

        // Check PIN verification result if available
        bool pinFailed = false;
        lock (_pinLock)
        {
            if (_pinVerificationResults.TryGetValue(evt.TransactionId, out var pinVerified))
            {
                _pinVerificationResults.Remove(evt.TransactionId);
                if (!pinVerified)
                {
                    pinFailed = true;
                }
            }
        }

        if (pinFailed)
        {
            _logger.LogWarning("Payment rejected for tx {TxId}: PIN verification failed", evt.TransactionId);
            ServiceMetrics.PaymentAuthorizationsTotal.WithLabels("rejected").Inc();
            await _eventBus.PublishAsync(new PaymentAuthorizedEvent(
                evt.TransactionId, false, "PIN verification failed", DateTime.UtcNow));
            return;
        }

        // Tuple deconstruction: Authorize() returns (bool, string).
        // "var (authorized, reason)" unpacks the tuple into two variables.
        var (authorized, reason) = _gateway.Authorize(amount, evt.RiskScore);

        ServiceMetrics.PaymentAuthorizationsTotal.WithLabels(authorized ? "approved" : "rejected").Inc();
        if (authorized) ServiceMetrics.PaymentAmountTotal.Inc((double)amount);

        _logger.LogInformation(
            "Payment processing for tx {TxId}: Authorized={Authorized}, Reason={Reason}",
            evt.TransactionId, authorized, reason);

        // Publish the authorization result as an event so downstream services
        // (like the Compliance/Audit Service) can record it.
        await _eventBus.PublishAsync(new PaymentAuthorizedEvent(
            evt.TransactionId, authorized, reason, DateTime.UtcNow));
    }
}
