namespace FinancialPlatform.EventInfrastructure.Serialization;

public record EventEnvelope(string EventType, string Payload);
