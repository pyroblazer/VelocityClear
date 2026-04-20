/*
 * InMemoryEventBus.cs
 *
 * PURPOSE:
 * Implements a simple in-process event bus. Events published within this
 * application are delivered to subscribers within the same process - no
 * external message broker (like RabbitMQ or Kafka) is needed. This is the
 * lightest-weight option, ideal for development, testing, or low-traffic
 * production scenarios.
 *
 * KEY C# CONCEPTS USED:
 *   - Dictionary<Type, ...> for mapping event types to handlers
 *   - Func<T, Task> delegate for async handler functions
 *   - async/await for non-blocking asynchronous execution
 *   - Task.CompletedTask for synchronous methods with async signatures
 *   - try/catch for exception handling
 *   - Expression-bodied members (=> syntax)
 */

// IEventBus defines the contract all bus implementations must follow:
// PublishAsync, SubscribeAsync, StartAsync, StopAsync.
using FinancialPlatform.Shared.Interfaces;

// ILogger<T> provides structured logging. The type parameter T (here
// InMemoryEventBus) becomes the "category" name shown in log output,
// making it easy to filter logs by source.
using Microsoft.Extensions.Logging;

namespace FinancialPlatform.EventInfrastructure.Bus;

// This class implements the IEventBus interface. The pub/sub pattern works
// like this: components call SubscribeAsync to register interest in an event
// type, and when PublishAsync is called, all registered handlers are invoked.
public class InMemoryEventBus : IEventBus
{
    // This dictionary is the core data structure. It maps:
    //   Key:   Type (the .NET type of an event, e.g., typeof(TransactionCreatedEvent))
    //   Value: List of handler functions that should be called when that event is published
    //
    // Func<object, Task> is a delegate (function pointer) representing a function that:
    //   - Takes one parameter of type "object" (the base type of all types in C#)
    //   - Returns a Task (a promise/future representing asynchronous completion)
    //
    // "readonly" means this dictionary reference cannot be reassigned after
    // construction, though its contents can still be modified.
    // "new()" at the end initializes the dictionary immediately.
    private readonly Dictionary<Type, List<Func<object, Task>>> _handlers = new();

    // The underscore prefix "_" is a widespread C# convention for private fields.
    // This logger instance is used throughout this class to write log messages.
    private readonly ILogger<InMemoryEventBus> _logger;

    // The constructor receives its dependencies through parameters. In production,
    // the dependency injection container automatically provides a logger instance.
    // This is called "constructor injection" and makes the class testable because
    // a mock logger can be passed in unit tests.
    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger;
    }

    // "async Task" means this is an asynchronous method that returns a Task.
    // A Task in C# is similar to a Promise in JavaScript - it represents work
    // that will complete in the future.
    //
    // The "where T : class" clause is a generic type constraint. It restricts
    // T to reference types only (classes, strings, arrays - not int, bool, etc.).
    // This ensures the event can be passed by reference and can be null-checked.
    public async Task PublishAsync<T>(T evt) where T : class
    {
        // LogDebug writes a message at the Debug severity level. Structured
        // logging placeholders like {EventType} are replaced with the value
        // of typeof(T).Name at runtime. typeof(T).Name gives the class name
        // as a string, e.g., "TransactionCreatedEvent".
        _logger.LogDebug("Publishing {EventType} via InMemory bus", typeof(T).Name);

        // TryGetValue safely looks up a key in the dictionary. It returns
        // true if found, and the "out var handlers" declares a variable that
        // receives the found value. This avoids a KeyNotFoundException that
        // the indexer (_handlers[typeof(T)]) would throw if the key is missing.
        if (_handlers.TryGetValue(typeof(T), out var handlers))
        {
            // Iterate over every handler registered for this event type.
            // "var" lets the compiler infer the type (here, Func<object, Task>).
            foreach (var handler in handlers)
            {
                try
                {
                    // "await" asynchronously waits for the handler to finish.
                    // If the handler throws an exception, it will be caught below.
                    // Using await (not .Result or .Wait()) avoids deadlocks.
                    await handler(evt);
                }
                catch (Exception ex)
                {
                    // Exception is the base class for all exceptions in C#.
                    // LogError logs the exception details including stack trace.
                    // Individual handler failures are caught so other handlers
                    // in the list still get executed (fault isolation).
                    _logger.LogError(ex, "Error handling {EventType}", typeof(T).Name);
                }
            }
        }
    }

    // This method returns Task (not async Task) because it performs no
    // asynchronous work - it just adds to a list. The async keyword adds
    // compiler-generated state machine overhead, so omitting it when
    // unnecessary is a common performance practice.
    public Task SubscribeAsync<T>(Func<T, Task> handler) where T : class
    {
        // Look up existing handlers for this event type.
        if (!_handlers.TryGetValue(typeof(T), out var handlers))
        {
            // "[]" is a collection expression (C# 12+) creating an empty list.
            // Equivalent to: new List<Func<object, Task>>()
            handlers = [];
            _handlers[typeof(T)] = handlers;
        }

        // The handler parameter is Func<T, Task> (typed), but the dictionary
        // stores Func<object, Task> (untyped). This lambda bridges the gap:
        //   e => handler((T)e)
        //   - "e" is the incoming object parameter
        //   - "(T)e" is an explicit cast from object to type T
        //   - The result of calling handler((T)e) is a Task, which matches
        //     the Func<object, Task> signature
        handlers.Add(e => handler((T)e));

        // Task.CompletedTask is a cached, already-completed Task instance.
        // Returning this instead of creating a new Task avoids unnecessary
        // allocations. It signals to the caller "the work is done" immediately.
        return Task.CompletedTask;
    }

    // Expression-bodied members use "=>" to define a method in a single
    // expression. These methods don't need to do anything for the in-memory
    // bus (no external connections to open or close), so they return
    // Task.CompletedTask immediately.
    // CancellationToken is a standard mechanism to signal cancellation;
    // "default" provides a none/empty token when the caller omits it.
    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;
}
