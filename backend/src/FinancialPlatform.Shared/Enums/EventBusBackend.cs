// ============================================================================
// EventBusBackend.cs - Event Bus Backend Selection Enum
//
// This file defines the available messaging backends for the event bus.
// The platform supports multiple backends and can switch between them
// adaptively based on system load (see AdaptiveEventBus for the switching logic).
// The backends range from simple (InMemory) to enterprise-grade (Kafka).
// ============================================================================

namespace FinancialPlatform.Shared.Enums;

// An "enum" (enumeration) defines a set of named constants. It is a value type
// backed by integers (0, 1, 2, 3...) by default. Enums are used to restrict a
// variable to a predefined set of values, making code self-documenting and
// type-safe. This is the same concept as enums in TypeScript, Java, or Python.
public enum EventBusBackend
{
    // Each member is an implicitly-numbered constant. InMemory = 0, Redis = 1, etc.
    // InMemory: events are passed between services in the same process - fastest,
    // but no persistence or cross-process communication.
    InMemory,

    // Redis: uses Redis Pub/Sub for lightweight, fast message passing between
    // processes. Good for moderate loads.
    Redis,

    // RabbitMQ: a full-featured message broker with routing, acknowledgments,
    // and durability. Good for moderate-to-high loads.
    RabbitMQ,

    // Kafka: a distributed streaming platform designed for very high throughput.
    // Best for the heaviest loads with event replay capabilities.
    Kafka
}
