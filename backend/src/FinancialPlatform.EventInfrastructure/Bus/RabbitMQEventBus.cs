/*
 * RabbitMQEventBus.cs
 *
 * PURPOSE:
 * Distributed event bus using RabbitMQ with a fanout exchange pattern.
 * All events are published to a single "events" exchange. Each service
 * declares its own durable queue bound to that exchange, ensuring fan-out:
 * every service type receives every event.
 *
 * PROTOCOL:
 *   Exchange:    "events" (fanout type) - broadcasts to all bound queues
 *   Queue:       "FinancialPlatform.{serviceName}.{eventType}" per service+event
 *   Publish:     BasicPublish to the "events" exchange
 *   Consume:     AsyncEventingBasicConsumer with manual ACK/NACK
 *   Acknowledge: BasicAck on success, BasicNack(requeue:true) on failure
 *
 * KEY RABBITMQ CONCEPTS:
 *   - Exchange: a routing mechanism. "fanout" broadcasts to all bound queues.
 *   - Queue: a buffer that stores messages until consumers process them.
 *   - Binding: links a queue to an exchange (with optional routing key).
 *   - Durable: survives broker restarts (persisted to disk).
 *   - ACK: confirms successful processing to the broker.
 *   - NACK with requeue: signals processing failure, message is redelivered.
 *
 * KEY C# CONCEPTS:
 *   - RabbitMQ.Client 7.x uses async-first API: IChannel (not IModel),
 *     CreateConnectionAsync, BasicPublishAsync, etc.
 *   - AsyncEventingBasicConsumer: push-based consumer - broker pushes messages
 *     to us, no polling required.
 */

using System.Collections.Concurrent;
using System.Text;
using FinancialPlatform.EventInfrastructure.Serialization;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FinancialPlatform.EventInfrastructure.Bus;

public class RabbitMQEventBus : IEventBus
{
    private readonly string _connectionString; // e.g., "amqp://guest:guest@rabbitmq:5672"
    private readonly string _serviceName;
    private readonly ILogger<RabbitMQEventBus> _logger;

    // IChannel is the async channel in RabbitMQ.Client 7.x (replaces IModel from 6.x).
    private IConnection? _connection;
    private IChannel? _channel;

    private readonly ConcurrentDictionary<Type, List<Func<object, Task>>> _handlers = new();
    private readonly List<Type> _subscribedEventTypes = [];

    public RabbitMQEventBus(string connectionString, string serviceName, ILogger<RabbitMQEventBus> logger)
    {
        _connectionString = connectionString;
        _serviceName = serviceName;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T evt) where T : class
    {
        if (_channel is null) return;

        var json = EventSerializer.SerializeEnvelope(evt);
        var body = Encoding.UTF8.GetBytes(json);

        // BasicPublishAsync sends a message to the exchange.
        // exchange: "events" (our fanout exchange)
        // routingKey: event type name (informational for fanout)
        // body: raw bytes of the serialized message
        await _channel.BasicPublishAsync(
            exchange: "events",
            routingKey: typeof(T).Name,
            body: body);

        _logger.LogDebug("Published {EventType} via RabbitMQ", typeof(T).Name);
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
        if (string.IsNullOrEmpty(_connectionString))
        {
            _logger.LogWarning("RabbitMQ connection string is empty, RabbitMQEventBus will not connect");
            return;
        }

        // ConnectionFactory creates the TCP connection to the RabbitMQ broker.
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_connectionString),
            AutomaticRecoveryEnabled = true, // Auto-reconnect on network failure
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        _logger.LogInformation("Connected to RabbitMQ at {Url}", _connectionString);

        // Declare the fanout exchange. durable: true = survives broker restarts.
        await _channel.ExchangeDeclareAsync(
            exchange: "events",
            type: ExchangeType.Fanout,
            durable: true,
            cancellationToken: ct);

        // Set prefetch count - each consumer gets at most 10 unacknowledged messages.
        // prefetchSize: 0 = no byte-size limit, prefetchCount: 10 = max 10 messages.
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: ct);

        foreach (var eventType in _subscribedEventTypes)
        {
            var queueName = $"FinancialPlatform.{_serviceName}.{eventType.Name}";

            // Declare a durable queue (persists across broker restarts).
            await _channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: ct);

            // Bind the queue to the fanout exchange.
            await _channel.QueueBindAsync(
                queue: queueName,
                exchange: "events",
                routingKey: eventType.Name,
                cancellationToken: ct);

            // AsyncEventingBasicConsumer fires the Received event when a message
            // arrives - this is push-based (no polling).
            var consumer = new AsyncEventingBasicConsumer(_channel);
            var capturedType = eventType;

            consumer.ReceivedAsync += async (model, ea) =>
            {
                await ProcessMessageAsync(ea, capturedType);
            };

            // Start consuming with manual acknowledgment (autoAck: false).
            await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: ct);

            _logger.LogInformation("Subscribed to {EventType} on queue {Queue} via RabbitMQ",
                eventType.Name, queueName);
        }
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs ea, Type eventType)
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.Span);
            var envelope = EventSerializer.DeserializeEnvelope(json);
            var payload = EventSerializer.DeserializePayload(envelope.Payload, eventType);

            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    await handler(payload);
                }
            }

            // Acknowledge successful processing.
            if (_channel is not null)
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {EventType} from RabbitMQ", eventType.Name);

            // NACK with requeue - message will be redelivered for retry.
            try
            {
                if (_channel is not null)
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
            catch (Exception nackEx)
            {
                _logger.LogError(nackEx, "Failed to NACK message");
            }
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(ct);
            _channel.Dispose();
        }
        if (_connection is not null)
        {
            await _connection.CloseAsync(ct);
            _connection.Dispose();
        }
    }

    private class AsyncHandlerWrapper<T>(Func<T, Task> handler) where T : class
    {
        public Task Invoke(object obj) => handler((T)obj);
    }
}
