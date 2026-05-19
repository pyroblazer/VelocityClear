using Confluent.Kafka;

namespace FinancialPlatform.AllServices.Services;

public class KafkaKeepAliveService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly ILogger<KafkaKeepAliveService> _logger;

    public KafkaKeepAliveService(
        IConfiguration config,
        ILogger<KafkaKeepAliveService> logger)
    {
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var brokers = _config["EventBus:KafkaBrokers"] ?? "localhost:9092";
        if (brokers == "localhost:9092")
        {
            _logger.LogInformation("Kafka keep-alive skipped: no remote Kafka configured");
            return;
        }

        var serviceName = _config["EventBus:ServiceName"] ?? "velocityclear-platform";
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = brokers,
            SecurityProtocol = SecurityProtocol.Ssl,
            ClientId = $"{serviceName}-keepalive",
            MessageTimeoutMs = 10000,
        };

        using var producer = new ProducerBuilder<Null, string>(producerConfig).Build();
        _logger.LogInformation("Kafka keep-alive started, pinging {Broker} every 5 min", brokers);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await producer.ProduceAsync("__health-check", new Message<Null, string>
                {
                    Value = $"{{\"service\":\"{serviceName}\",\"ts\":\"{DateTime.UtcNow:O}\"}}"
                }, stoppingToken);
                _logger.LogDebug("Kafka keep-alive ping sent");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kafka keep-alive ping failed");
            }
        }
    }
}
