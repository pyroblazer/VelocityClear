using FinancialPlatform.EventInfrastructure.Serialization;
using FinancialPlatform.Shared.Events;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class EventSerializerTests
{
    [Fact]
    public void SerializeEnvelope_ProducesValidJson_WithCamelCase()
    {
        var evt = new TransactionCreatedEvent("tx_1", "user_1", 100m, "USD", new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        var json = EventSerializer.SerializeEnvelope(evt);

        // The outer envelope should contain the event type.
        Assert.Contains("\"eventType\":\"TransactionCreatedEvent\"", json);
        // The payload is a nested JSON string - verify via round-trip deserialization.
        var envelope = EventSerializer.DeserializeEnvelope(json);
        Assert.Equal("TransactionCreatedEvent", envelope.EventType);
        Assert.Contains("tx_1", envelope.Payload);
        Assert.Contains("user_1", envelope.Payload);
        Assert.Contains("100", envelope.Payload);
    }

    [Fact]
    public void DeserializeEnvelope_RoundTrips_Successfully()
    {
        var evt = new TransactionCreatedEvent("tx_42", "user_2", 250m, "EUR", DateTime.UtcNow);
        var json = EventSerializer.SerializeEnvelope(evt);
        var envelope = EventSerializer.DeserializeEnvelope(json);

        Assert.Equal("TransactionCreatedEvent", envelope.EventType);
        Assert.NotEmpty(envelope.Payload);
    }

    [Fact]
    public void DeserializePayload_ReconstructsOriginalEvent()
    {
        var original = new TransactionCreatedEvent("tx_99", "user_3", 999.99m, "GBP", DateTime.UtcNow);
        var json = EventSerializer.SerializeEnvelope(original);
        var envelope = EventSerializer.DeserializeEnvelope(json);
        var deserialized = (TransactionCreatedEvent)EventSerializer.DeserializePayload(envelope.Payload, typeof(TransactionCreatedEvent));

        Assert.Equal(original.TransactionId, deserialized.TransactionId);
        Assert.Equal(original.UserId, deserialized.UserId);
        Assert.Equal(original.Amount, deserialized.Amount);
        Assert.Equal(original.Currency, deserialized.Currency);
    }

    [Fact]
    public void DeserializeEnvelope_ThrowsOnInvalidJson()
    {
        Assert.ThrowsAny<Exception>(() => EventSerializer.DeserializeEnvelope("not valid json"));
    }

    [Fact]
    public void SerializeEnvelope_AllEventTypes_RoundTrip()
    {
        var txEvent = new TransactionCreatedEvent("tx_1", "u1", 10m, "USD", DateTime.UtcNow);
        var riskEvent = new RiskEvaluatedEvent("tx_1", 75, "HIGH", ["VELOCITY"], DateTime.UtcNow);
        var paymentEvent = new PaymentAuthorizedEvent("tx_1", true, "Approved", DateTime.UtcNow);
        var auditEvent = new AuditLoggedEvent("audit_1", "TransactionCreated", "abc123hash", DateTime.UtcNow);

        foreach (var evt in new object[] { txEvent, riskEvent, paymentEvent, auditEvent })
        {
            var type = evt.GetType();
            var serializeMethod = typeof(EventSerializer).GetMethod(nameof(EventSerializer.SerializeEnvelope))!;
            var generic = serializeMethod.MakeGenericMethod(type);
            var json = (string)generic.Invoke(null, [evt])!;

            var envelope = EventSerializer.DeserializeEnvelope(json);
            Assert.Equal(type.Name, envelope.EventType);

            var payload = EventSerializer.DeserializePayload(envelope.Payload, type);
            Assert.IsType(type, payload);
        }
    }
}
