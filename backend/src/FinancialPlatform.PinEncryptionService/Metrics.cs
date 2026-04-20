using Prometheus;
using PMetrics = Prometheus.Metrics;

namespace FinancialPlatform.PinEncryptionService;

public static class ServiceMetrics
{
    public static readonly Counter HsmOperationsTotal = PMetrics
        .CreateCounter("hsm_operations_total", "Total HSM operations performed", "operation");

    public static readonly Gauge EventBusCurrentBackend = PMetrics
        .CreateGauge("eventbus_current_backend", "Current event bus backend: 0=InMemory, 1=Redis, 2=RabbitMQ, 3=Kafka");
}
