using FinancialPlatform.Shared.Events;

namespace FinancialPlatform.EventInfrastructure.Serialization;

public static class EventTypeResolver
{
    private static readonly Dictionary<string, Type> _knownTypes = new()
    {
        [nameof(TransactionCreatedEvent)] = typeof(TransactionCreatedEvent),
        [nameof(RiskEvaluatedEvent)] = typeof(RiskEvaluatedEvent),
        [nameof(PaymentAuthorizedEvent)] = typeof(PaymentAuthorizedEvent),
        [nameof(AuditLoggedEvent)] = typeof(AuditLoggedEvent),
        [nameof(PinVerifiedEvent)] = typeof(PinVerifiedEvent)
    };

    public static Type Resolve(string typeName)
        => _knownTypes.TryGetValue(typeName, out var type)
            ? type
            : throw new InvalidOperationException($"Unknown event type: {typeName}");
}
