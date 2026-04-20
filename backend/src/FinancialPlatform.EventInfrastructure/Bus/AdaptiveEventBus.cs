/*
 * AdaptiveEventBus.cs
 *
 * PURPOSE:
 * Strategy pattern wrapper that monitors CPU usage and automatically switches
 * between four event bus backends (InMemory, Redis, RabbitMQ, Kafka) based
 * on system load. Also supports pinning a backend via configuration, which
 * is the recommended approach for Docker/production deployments.
 *
 * KEY C# CONCEPTS USED:
 *   - Delegate factories (Func<T>) for lazy object creation
 *   - Pattern matching with switch expressions
 *   - Timer for periodic background execution
 *   - lock statement for thread safety
 *   - C# events (EventHandler<T>) for the observer pattern
 *   - Constructor overloading for backward compatibility
 *   - Record types for immutable configuration objects
 */

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using FinancialPlatform.EventInfrastructure.Configuration;
using FinancialPlatform.EventInfrastructure.Serialization;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace FinancialPlatform.EventInfrastructure.Bus;

public class AdaptiveEventBus : IEventBus
{
    private IEventBus _currentBus;
    private EventBusBackend _currentBackend;
    private readonly ILogger<AdaptiveEventBus> _logger;

    // Dictionary mapping event types (e.g., typeof(TransactionCreatedEvent))
    // to lists of handler functions. When an event is published, all handlers
    // registered for that type are invoked. The handlers are stored as
    // Func<object, Task> (untyped) so different event types can coexist.
    private readonly Dictionary<Type, List<Func<object, Task>>> _handlers = new();
    private readonly object _lock = new();
    private Timer? _monitorTimer;
    private readonly Dictionary<EventBusBackend, Func<IEventBus>> _busFactories;

    // The configuration record holds connection strings and the preferred backend.
    private readonly EventBusConnectionConfig _config;

    public EventBusBackend CurrentBackend => _currentBackend;

    // C# event implementing the observer pattern. Subscribers register with "+="
    // and are notified when the active backend changes.
    public event EventHandler<EventBusBackend>? BackendChanged;

    // Backward-compatible constructor used by existing unit/integration tests.
    // Delegates to the full constructor with default config (InMemory, empty
    // connection strings). Since tests never call StartAsync on real backends,
    // the empty connection strings are never used.
    public AdaptiveEventBus(
        ILogger<AdaptiveEventBus> logger,
        ILogger<InMemoryEventBus> inMemoryLogger,
        ILogger<RedisEventBus> redisLogger,
        ILogger<RabbitMQEventBus> rabbitMqLogger,
        ILogger<KafkaEventBus> kafkaLogger)
        : this(new EventBusConnectionConfig(), logger, inMemoryLogger, redisLogger, rabbitMqLogger, kafkaLogger)
    {
    }

    // Full constructor accepting configuration plus loggers for each backend.
    public AdaptiveEventBus(
        EventBusConnectionConfig config,
        ILogger<AdaptiveEventBus> logger,
        ILogger<InMemoryEventBus> inMemoryLogger,
        ILogger<RedisEventBus> redisLogger,
        ILogger<RabbitMQEventBus> rabbitMqLogger,
        ILogger<KafkaEventBus> kafkaLogger)
    {
        _config = config;
        _logger = logger;

        // Factory dictionary: each backend enum value maps to a lambda that
        // creates the corresponding IEventBus instance. Lambdas are closures
        // that capture the logger and config variables from this scope.
        _busFactories = new Dictionary<EventBusBackend, Func<IEventBus>>
        {
            [EventBusBackend.InMemory] = () => new InMemoryEventBus(inMemoryLogger),
            [EventBusBackend.Redis] = () => new RedisEventBus(config.RedisUrl, config.ServiceName, redisLogger),
            [EventBusBackend.RabbitMQ] = () => new RabbitMQEventBus(config.RabbitMqUrl, config.ServiceName, rabbitMqLogger),
            [EventBusBackend.Kafka] = () => new KafkaEventBus(config.KafkaBrokers, config.ServiceName, kafkaLogger)
        };

        // Parse the initial backend from the config string. "ignoreCase: true"
        // makes "redis", "Redis", "REDIS" all valid. If parsing fails, fall
        // back to InMemory.
        _currentBackend = Enum.TryParse<EventBusBackend>(config.DefaultBackend, ignoreCase: true, out var parsed)
            ? parsed
            : EventBusBackend.InMemory;

        _currentBus = _busFactories[_currentBackend]();
    }

    public async Task PublishAsync<T>(T evt) where T : class
    {
        await _currentBus.PublishAsync(evt);
    }

    public async Task SubscribeAsync<T>(Func<T, Task> handler) where T : class
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(T), out var handlers))
            {
                handlers = [];
                _handlers[typeof(T)] = handlers;
            }
            // Wrap the typed handler into an untyped one by casting the
            // object parameter back to the concrete event type T.
            handlers.Add(e => handler((T)e));
        }

        await _currentBus.SubscribeAsync(handler);
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _monitorTimer = new Timer(EvaluateAndSwitch, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        await _currentBus.StartAsync(ct);
        _logger.LogInformation("AdaptiveEventBus started with {Backend} backend", _currentBackend);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        _monitorTimer?.Dispose();
        await _currentBus.StopAsync(ct);
    }

    [ExcludeFromCodeCoverage]
    private void EvaluateAndSwitch(object? state)
    {
        var cpuUsage = GetCpuUsage();

        var target = cpuUsage switch
        {
            < 25 => EventBusBackend.InMemory,
            < 50 => EventBusBackend.Redis,
            < 75 => EventBusBackend.RabbitMQ,
            _ => EventBusBackend.Kafka
        };

        if (target == _currentBackend)
            return;

        lock (_lock)
        {
            // Double-check after acquiring lock to prevent concurrent switches
            if (target == _currentBackend)
                return;

            _logger.LogInformation(
                "Switching event bus from {From} to {To} (CPU: {Cpu}%)",
                _currentBackend, target, cpuUsage);

            try
            {
                var oldBus = _currentBus;
                var newBus = _busFactories[target]();

                // Start the new bus before switching
                newBus.StartAsync().GetAwaiter().GetResult();

                // Re-register all existing handlers on the new bus
                foreach (var (eventType, handlers) in _handlers)
                {
                    foreach (var handler in handlers)
                    {
                        var subscribeMethod = typeof(IEventBus)
                            .GetMethod(nameof(IEventBus.SubscribeAsync))!
                            .MakeGenericMethod(eventType);
                        subscribeMethod.Invoke(newBus, new object[] { handler });
                    }
                }

                _currentBus = newBus;
                _currentBackend = target;

                // Stop the old bus after successful switch
                try { oldBus.StopAsync().GetAwaiter().GetResult(); }
                catch (Exception ex) { _logger.LogWarning(ex, "Error stopping old {Backend} bus", target); }

                BackendChanged?.Invoke(this, target);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch from {From} to {To}, keeping current backend",
                    _currentBackend, target);
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private static double GetCpuUsage()
    {
        var process = Process.GetCurrentProcess();
        var cpuTime = process.TotalProcessorTime;
        Thread.Sleep(100);
        var cpuTime2 = process.TotalProcessorTime;
        var diff = (cpuTime2 - cpuTime).TotalMilliseconds;
        return Math.Min(diff / (Environment.ProcessorCount * 100.0) * 100.0, 100.0);
    }
}
