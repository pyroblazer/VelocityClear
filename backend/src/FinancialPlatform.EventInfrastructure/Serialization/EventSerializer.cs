using System.Text.Json;

namespace FinancialPlatform.EventInfrastructure.Serialization;

public static class EventSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string SerializeEnvelope<T>(T evt) where T : class
    {
        var envelope = new EventEnvelope(typeof(T).Name, JsonSerializer.Serialize(evt, _options));
        return JsonSerializer.Serialize(envelope, _options);
    }

    public static EventEnvelope DeserializeEnvelope(string json)
        => JsonSerializer.Deserialize<EventEnvelope>(json, _options)
           ?? throw new InvalidOperationException("Failed to deserialize event envelope");

    public static object DeserializePayload(string payload, Type eventType)
        => JsonSerializer.Deserialize(payload, eventType, _options)
           ?? throw new InvalidOperationException($"Failed to deserialize {eventType.Name}");
}
