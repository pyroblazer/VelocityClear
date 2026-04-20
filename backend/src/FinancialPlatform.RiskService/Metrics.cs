using Prometheus;
using PMetrics = Prometheus.Metrics;

namespace FinancialPlatform.RiskService;

public static class ServiceMetrics
{
    public static readonly Gauge RiskScore = PMetrics
        .CreateGauge("risk_score", "Latest risk evaluation score", "risk_level");

    public static readonly Counter RiskEvaluationsTotal = PMetrics
        .CreateCounter("risk_evaluations_total", "Total risk evaluations performed", "risk_level");

    public static readonly Gauge EventBusCurrentBackend = PMetrics
        .CreateGauge("eventbus_current_backend", "Current event bus backend: 0=InMemory, 1=Redis, 2=RabbitMQ, 3=Kafka");
}
