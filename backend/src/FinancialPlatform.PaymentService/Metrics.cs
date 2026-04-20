using Prometheus;
using PMetrics = Prometheus.Metrics;

namespace FinancialPlatform.PaymentService;

public static class ServiceMetrics
{
    public static readonly Counter PaymentAuthorizationsTotal = PMetrics
        .CreateCounter("payment_authorizations_total", "Total payment authorization decisions", "result");

    public static readonly Counter PaymentAmountTotal = PMetrics
        .CreateCounter("payment_amount_total", "Total dollar amount of approved payments");

    public static readonly Gauge EventBusCurrentBackend = PMetrics
        .CreateGauge("eventbus_current_backend", "Current event bus backend: 0=InMemory, 1=Redis, 2=RabbitMQ, 3=Kafka");
}
