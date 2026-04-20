// ============================================================================
// IEventBus.cs - Event Bus Interface
//
// This file defines the contract (interface) for the event bus, which is the
// backbone of asynchronous communication between microservices in this platform.
// Any class that wants to act as an event bus (e.g., in-memory, Redis, RabbitMQ,
// Kafka) must implement all the methods declared here.
//
// Think of this as a "publisher/subscriber" pattern: services publish events,
// and other services subscribe to receive and handle those events.
// ============================================================================

// A "namespace" groups related types together, similar to packages in Java or
// modules in TypeScript. It prevents naming collisions across the codebase.
namespace FinancialPlatform.Shared.Interfaces;

// An "interface" in C# defines a contract - a set of methods and properties
// that any implementing class MUST provide. This is the same concept as
// interfaces in Java or TypeScript. You cannot instantiate an interface directly;
// you can only instantiate a class that implements it.
public interface IEventBus
{
    // "Task" is C#'s representation of an asynchronous operation, equivalent to
    // "Promise" in JavaScript/TypeScript. The "async/await" pattern in C# works
    // with Task objects. Methods returning Task run asynchronously and the caller
    // can "await" their completion.
    //
    // "<T>" is a generic type parameter - similar to generics in Java or
    // TypeScript's <T>. The caller decides what type T is at each call site.
    //
    // "where T : class" is a generic constraint. It restricts T to reference types
    // only (classes, not value types like int/bool). This ensures the event object
    // is always a reference type.
    Task PublishAsync<T>(T evt) where T : class;

    // "Func<T, Task>" is a delegate type - essentially a function pointer that
    // takes a parameter of type T and returns a Task. This is similar to passing
    // a callback function in JavaScript: (evt) => { ... }.
    //
    // The handler parameter is the callback that will be invoked when an event
    // of type T is received.
    Task SubscribeAsync<T>(Func<T, Task> handler) where T : class;

    // "CancellationToken" is a mechanism to signal that an asynchronous operation
    // should be cancelled. It is cooperative - the operation checks the token
    // periodically and stops if cancellation is requested. Similar to AbortController
    // in JavaScript.
    //
    // "ct = default" provides a default parameter value. "default" for a struct
    // type like CancellationToken produces an empty/default token (never cancelled).
    Task StartAsync(CancellationToken ct = default);

    // StopAsync gracefully shuts down the event bus, using the same cancellation
    // token pattern as StartAsync.
    Task StopAsync(CancellationToken ct = default);
}
