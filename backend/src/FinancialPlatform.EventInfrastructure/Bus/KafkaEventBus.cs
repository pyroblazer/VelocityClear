/*
 * KafkaEventBus.cs
 *
 * PURPOSE:
 * Distributed event bus using Apache Kafka topics and consumer groups.
 * Each event type maps to its own topic (e.g., "events-TransactionCreatedEvent").
 * Services consume via consumer groups, providing fan-out (every service type
 * gets every event) and load balancing (instances within a service share load).
 *
 * PROTOCOL:
 *   Topic:       "events-{EventType}" (one topic per event type)
 *   Publish:     ProduceAsync to the topic
 *   Consume:     Consume() - BLOCKING call, waits for next message
 *   Acknowledge: Commit() after successful handler execution
 *
 * KEY KAFKA CONCEPTS:
 *   - Topic: a category/feed to which messages are published (like a stream).
 *   - Consumer Group: consumers cooperating on a topic. Each message goes to
 *     only one consumer within a group. Different groups each get all messages.
 *   - Offset: a sequential ID per message within a partition.
 *   - Commit: marks an offset as processed. With EnableAutoCommit=false, we
 *     commit manually only after successful processing (at-least-once delivery).
 *   - AutoOffsetReset.Earliest: start from oldest message if no committed offset.
 *
 * KEY C# CONCEPTS:
 *   - Confluent.Kafka wraps librdkafka (C library) for high performance.
 *   - IProducer<string, string> - typed producer (key type, value type).
 *   - IConsumer<string, string> - typed consumer with blocking Consume().
 *   - consumer.Consume(ct) BLOCKS the thread until a message arrives or
 *     cancellation is requested - true streaming, zero polling overhead.
 */

using System.Collections.Concurrent;
using Confluent.Kafka;
using FinancialPlatform.EventInfrastructure.Serialization;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace FinancialPlatform.EventInfrastructure.Bus;

public class KafkaEventBus : IEventBus
{
    private readonly string _brokers; // e.g., "kafka:9092"
    private readonly string _serviceName;
    private readonly ILogger<KafkaEventBus> _logger;

    private IProducer<string, string>? _producer;
    private readonly List<IConsumer<string, string>> _consumers = [];
    private readonly List<Task> _consumerTasks = [];

    private readonly ConcurrentDictionary<Type, List<Func<object, Task>>> _handlers = new();
    private readonly List<Type> _subscribedEventTypes = [];

    private CancellationTokenSource? _cts;

    public KafkaEventBus(string brokers, string serviceName, ILogger<KafkaEventBus> logger)
    {
        _brokers = brokers;
        _serviceName = serviceName;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T evt) where T : class
    {
        if (_producer is null) return;

        var topic = $"events-{typeof(T).Name}";
        var json = EventSerializer.SerializeEnvelope(evt);

        // ProduceAsync sends a message and waits for broker acknowledgment.
        // Key: unique GUID for partition routing (even distribution).
        // Value: the serialized event envelope.
        var result = await _producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = json
        });

        _logger.LogDebug("Published {EventType} to Kafka topic {Topic} [partition {Partition}, offset {Offset}]",
            typeof(T).Name, topic, result.Partition, result.Offset);
    }

    public Task SubscribeAsync<T>(Func<T, Task> handler) where T : class
    {
        var type = typeof(T);

        _handlers.AddOrUpdate(
            type,
            _ => [new AsyncHandlerWrapper<T>(handler).Invoke],
            (_, existing) => { existing.Add(new AsyncHandlerWrapper<T>(handler).Invoke); return existing; }
        );

        if (!_subscribedEventTypes.Contains(type))
            _subscribedEventTypes.Add(type);

        return Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_brokers))
        {
            _logger.LogWarning("Kafka brokers string is empty, KafkaEventBus will not connect");
            return;
        }

        // Create the producer with durability guarantees.
        // Acks.All = wait for acknowledgment from all in-sync replicas.
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _brokers,
            ClientId = $"{_serviceName}-producer",
            Acks = Acks.All
        }).Build();

        _logger.LogInformation("Kafka producer connected to {Brokers}", _brokers);

        _cts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        // Create a consumer for each subscribed event type.
        foreach (var eventType in _subscribedEventTypes)
        {
            var topic = $"events-{eventType.Name}";

            // GroupId = service name for fan-out across services.
            // EnableAutoCommit = false for manual offset commit (at-least-once).
            // AutoOffsetReset.Earliest = read from start if no committed offset.
            var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = _brokers,
                GroupId = _serviceName,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                ClientId = $"{_serviceName}-{eventType.Name}-consumer"
            }).Build();

            consumer.Subscribe(topic);
            _consumers.Add(consumer);

            // Local copies for the closure (avoids capturing loop variable).
            var capturedType = eventType;
            var capturedConsumer = consumer;

            var task = Task.Run(() => ConsumeLoopAsync(capturedConsumer, capturedType, linkedCts.Token), linkedCts.Token);
            _consumerTasks.Add(task);

            _logger.LogInformation("Kafka consumer started for topic {Topic} in group {Group}",
                topic, _serviceName);
        }

        await Task.CompletedTask;
    }

    private async Task ConsumeLoopAsync(IConsumer<string, string> consumer, Type eventType, CancellationToken ct)
    {
        _logger.LogInformation("Kafka consume loop started for {EventType}", eventType.Name);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Consume(ct) BLOCKS until a message arrives or cancellation.
                // This is true streaming consumption - zero polling overhead.
                var result = consumer.Consume(ct);

                try
                {
                    var envelope = EventSerializer.DeserializeEnvelope(result.Message.Value);
                    var payload = EventSerializer.DeserializePayload(envelope.Payload, eventType);

                    if (_handlers.TryGetValue(eventType, out var handlers))
                    {
                        foreach (var handler in handlers)
                        {
                            await handler(payload);
                        }
                    }

                    // Commit offset after all handlers succeed.
                    consumer.Commit(result);

                    _logger.LogDebug("Committed offset for {EventType} at {Offset}",
                        eventType.Name, result.Offset);
                }
                catch (Exception ex)
                {
                    // Don't commit - message will be redelivered on restart.
                    _logger.LogError(ex, "Error processing {EventType} from Kafka at offset {Offset}, not committing",
                        eventType.Name, result.Offset);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka consume loop crashed for {EventType}", eventType.Name);
        }
        finally
        {
            _logger.LogInformation("Kafka consume loop stopped for {EventType}", eventType.Name);
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();

        if (_consumerTasks.Count > 0)
        {
            var timeout = Task.Delay(TimeSpan.FromSeconds(10), ct);
            await Task.WhenAny(Task.WhenAll(_consumerTasks), timeout);
        }

        foreach (var consumer in _consumers)
            consumer.Dispose();

        _producer?.Dispose();
    }

    private class AsyncHandlerWrapper<T>(Func<T, Task> handler) where T : class
    {
        public Task Invoke(object obj) => handler((T)obj);
    }
}
