namespace FinancialPlatform.EventInfrastructure.Configuration;

public record EventBusConnectionConfig(
    string DefaultBackend = "InMemory",
    string RedisUrl = "",
    string RabbitMqUrl = "",
    string KafkaBrokers = "",
    string ServiceName = "unknown"
);
