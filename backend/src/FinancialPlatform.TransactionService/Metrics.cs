using Prometheus;
using PMetrics = Prometheus.Metrics;

namespace FinancialPlatform.TransactionService;

public static class ServiceMetrics
{
    public static readonly Counter TransactionsCreatedTotal = PMetrics
        .CreateCounter("transactions_created_total", "Total transactions created");

    public static readonly Counter EventsPublishedTotal = PMetrics
        .CreateCounter("events_published_total", "Total events published to the event bus", "event_type");

    public static readonly Gauge EventBusCurrentBackend = PMetrics
        .CreateGauge("eventbus_current_backend", "Current event bus backend: 0=InMemory, 1=Redis, 2=RabbitMQ, 3=Kafka");
}
