using Prometheus;
using PMetrics = Prometheus.Metrics;

namespace FinancialPlatform.ComplianceService;

public static class ServiceMetrics
{
    public static readonly Counter AuditLogsCreatedTotal = PMetrics
        .CreateCounter("audit_logs_created_total", "Total audit log entries created", "event_type");

    public static readonly Gauge EventBusCurrentBackend = PMetrics
        .CreateGauge("eventbus_current_backend", "Current event bus backend: 0=InMemory, 1=Redis, 2=RabbitMQ, 3=Kafka");
}
