/*
 * RedisEventBus.cs
 *
 * PURPOSE:
 * Distributed event bus using Redis Streams. Each event type gets its own
 * Redis Stream (e.g., "events:TransactionCreatedEvent"). Services consume
 * messages via consumer groups (XREADGROUP), ensuring fan-out semantics:
 * every service type gets its own copy of each event, while multiple instances
 * of the same service compete for messages within their shared group.
 *
 * PROTOCOL:
 *   Publish:     XADD stream-key * type <typename> payload <json>
 *   Consume:     XREADGROUP GROUP <service-name> <instance-id> COUNT 10 STREAMS stream-key >
 *   Acknowledge: XACK stream-key <service-name> <message-id>
 *
 * KEY REDIS CONCEPTS:
 *   - Stream: an append-only log of entries (similar to Kafka topics)
 *   - Consumer Group: a named group of consumers sharing a stream.
 *     Each message is delivered to only one consumer in the group.
 *   - XREADGROUP with ">" reads only NEW messages never seen by the group.
 *   - XACK confirms successful processing (removes from pending list).
 *
 * KEY C# CONCEPTS:
 *   - async/await for non-blocking I/O
 *   - CancellationToken for graceful shutdown
 *   - ConcurrentDictionary for thread-safe handler storage
 *   - Task.Run for background execution on the thread pool
 */

using System.Collections.Concurrent;
using FinancialPlatform.EventInfrastructure.Serialization;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FinancialPlatform.EventInfrastructure.Bus;

public class RedisEventBus : IEventBus
{
    // Connection string, e.g., "redis:6379" or "localhost:6379"
    private readonly string _connectionString;

    // Service identifier used as the consumer group name (e.g., "risk-service").
    private readonly string _serviceName;
    private readonly ILogger<RedisEventBus> _logger;

    // Lazy-initialized Redis connection. ConnectionMultiplexer is the main entry
    // point for StackExchange.Redis - it manages connection pooling and reconnection.
    private ConnectionMultiplexer? _mux;
    private IDatabase? _db;

    // Thread-safe dictionary mapping event types to handler lists.
    // ConcurrentDictionary is safe for concurrent reads/writes without explicit locks.
    private readonly ConcurrentDictionary<Type, List<Func<object, Task>>> _handlers = new();

    // ConcurrentBag is a thread-safe unordered collection (like a bag/multiset).
    // Used here to accumulate stream keys that have been subscribed to.
    private readonly ConcurrentBag<string> _subscribedStreams = [];

    // CancellationTokenSource signals the consumer loop to stop.
    private CancellationTokenSource? _cts;
    private Task? _consumerTask;

    // Unique instance ID for this consumer within the group.
    // Guid.NewGuid creates a globally unique 128-bit identifier.
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    public RedisEventBus(string connectionString, string serviceName, ILogger<RedisEventBus> logger)
    {
        _connectionString = connectionString;
        _serviceName = serviceName;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T evt) where T : class
    {
        if (_db is null) return;

        // The stream key is "events:{TypeName}", e.g., "events:TransactionCreatedEvent".
        var streamKey = $"events:{typeof(T).Name}";

        // Serialize the event to an envelope containing the type name and JSON payload.
        var json = EventSerializer.SerializeEnvelope(evt);

        // StreamAddAsync wraps Redis XADD - appends an entry to the stream.
        // NameValueEntry[] is an array of field-value pairs stored in the entry.
        await _db.StreamAddAsync(streamKey,
        [
            new NameValueEntry("type", typeof(T).Name),
            new NameValueEntry("payload", json)
        ]);

        _logger.LogDebug("Published {EventType} to Redis stream {Stream}", typeof(T).Name, streamKey);
    }

    public Task SubscribeAsync<T>(Func<T, Task> handler) where T : class
    {
        var type = typeof(T);
        var streamKey = $"events:{type.Name}";
        _subscribedStreams.Add(streamKey);

        // AddOrUpdate is an atomic operation on ConcurrentDictionary:
        // - If key doesn't exist: call the factory to create a new list
        // - If key exists: call the update function to add to the existing list
        _handlers.AddOrUpdate(
            type,
            _ => [new AsyncHandlerWrapper<T>(handler).Invoke],
            (_, existing) => { existing.Add(new AsyncHandlerWrapper<T>(handler).Invoke); return existing; }
        );

        return Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            _logger.LogWarning("Redis connection string is empty, RedisEventBus will not connect");
            return;
        }

        // Connect to Redis. ConfigurationOptions.Parse handles "host:port" format.
        var config = ConfigurationOptions.Parse(_connectionString);
        config.AbortOnConnectFail = false; // Don't throw if Redis isn't ready yet
        config.ConnectRetry = 3;
        config.ConnectTimeout = 5000;

        _mux = await ConnectionMultiplexer.ConnectAsync(config);
        _db = _mux.GetDatabase();

        _logger.LogInformation("Connected to Redis at {Url}", _connectionString);

        // Create consumer groups for each subscribed stream.
        foreach (var streamKey in _subscribedStreams)
        {
            try
            {
                // StreamCreateConsumerGroupAsync wraps Redis XGROUP CREATE.
                // "0-0" = start reading from the beginning of the stream.
                // createStream: true = auto-create the stream if it doesn't exist.
                await _db.StreamCreateConsumerGroupAsync(streamKey, _serviceName, "0-0", createStream: true);
                _logger.LogInformation("Created consumer group {Group} on stream {Stream}", _serviceName, streamKey);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
            {
                // BUSYGROUP = consumer group already exists - this is fine.
                _logger.LogDebug("Consumer group {Group} already exists on {Stream}", _serviceName, streamKey);
            }
        }

        // Start the background consumer loop on the thread pool.
        _cts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        _consumerTask = Task.Run(() => ConsumeLoopAsync(linkedCts.Token), linkedCts.Token);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();

        // Wait for the consumer task to finish, with a 5-second timeout.
        if (_consumerTask is not null)
        {
            var timeout = Task.Delay(TimeSpan.FromSeconds(5), ct);
            await Task.WhenAny(_consumerTask, timeout);
        }

        if (_mux is not null)
        {
            await _mux.CloseAsync();
            _mux.Dispose();
        }
    }

    private async Task ConsumeLoopAsync(CancellationToken ct)
    {
        _logger.LogInformation("Redis consumer loop started for service {Service}", _serviceName);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                foreach (var streamKey in _subscribedStreams)
                {
                    if (_db is null || ct.IsCancellationRequested) break;

                    // StreamReadGroupAsync wraps XREADGROUP: reads messages from the
                    // stream that haven't been delivered to this consumer yet.
                    // ">" means "give me only new messages" (not pending ones).
                    // count: 10 limits how many messages to fetch per call.
                    var entries = await _db.StreamReadGroupAsync(
                        streamKey, _serviceName, _instanceId, ">", count: 10);

                    foreach (var entry in entries)
                    {
                        if (ct.IsCancellationRequested) break;
                        await ProcessEntryAsync(streamKey, entry);
                    }
                }

                // Brief pause to avoid CPU spinning when no messages are available.
                await Task.Delay(100, ct);
            }
            catch (OperationCanceledException)
            {
                break; // Expected during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Redis consumer loop");
                await Task.Delay(1000, ct); // Back off on error
            }
        }

        _logger.LogInformation("Redis consumer loop stopped for service {Service}", _serviceName);
    }

    private async Task ProcessEntryAsync(string streamKey, StreamEntry entry)
    {
        try
        {
            var typeName = entry["type"].ToString();
            var payloadJson = entry["payload"].ToString();

            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(payloadJson)) return;

            var eventType = EventTypeResolver.Resolve(typeName);
            var envelope = EventSerializer.DeserializeEnvelope(payloadJson);
            var payload = EventSerializer.DeserializePayload(envelope.Payload, eventType);

            // Invoke all registered handlers for this event type.
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        await handler(payload);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling {EventType} from Redis", eventType.Name);
                        return; // Don't ACK - message stays pending for reprocessing
                    }
                }
            }

            // ACK the message only after all handlers succeed.
            // StreamAcknowledgeAsync wraps Redis XACK.
            if (_db is not null)
                await _db.StreamAcknowledgeAsync(streamKey, _serviceName, entry.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Redis stream entry {Id}", entry.Id);
        }
    }

    // Wrapper to convert a typed handler (Func<T, Task>) into an untyped one
    // (Func<object, Task>) by capturing the type cast in a closure.
    private class AsyncHandlerWrapper<T>(Func<T, Task> handler) where T : class
    {
        public Task Invoke(object obj) => handler((T)obj);
    }
}
