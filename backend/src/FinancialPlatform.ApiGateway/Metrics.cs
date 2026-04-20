using Prometheus;
using PMetrics = Prometheus.Metrics;

namespace FinancialPlatform.ApiGateway;

public static class ServiceMetrics
{
    public static readonly Gauge SseActiveConnections = PMetrics
        .CreateGauge("sse_active_connections", "Number of active SSE connections");

    public static readonly Counter JwtAuthAttemptsTotal = PMetrics
        .CreateCounter("jwt_auth_attempts_total", "Total JWT authentication attempts", "result");

    public static readonly Gauge EventBusCurrentBackend = PMetrics
        .CreateGauge("eventbus_current_backend", "Current event bus backend: 0=InMemory, 1=Redis, 2=RabbitMQ, 3=Kafka");
}
