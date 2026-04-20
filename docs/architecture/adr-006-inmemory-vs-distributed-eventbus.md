# ADR-006: InMemory EventBus for Development vs. Distributed Backends for Production

**Status:** Accepted
**Date:** 2026-04-22
**ISO 27001 Controls:** A.12.1.2 (Change management), A.14.2.2 (System change control)
**ISO 9001 Clauses:** 7.5

## Context

The platform needs an event delivery mechanism that works both in a single-process local development environment and in a distributed multi-service production deployment. Developers need fast feedback loops during local development, while production requires durability, cross-service delivery, and recovery from failures.

The core tension is between latency and durability. In-memory event delivery provides sub-millisecond latency but cannot propagate events across process boundaries or survive process restarts. Distributed message brokers (Redis, RabbitMQ, Kafka) provide durability and cross-service delivery but add network latency, configuration complexity, and operational overhead.

## Decision

We implemented two categories of event bus backends behind a common `IEventBus` interface:

1. **InMemory EventBus** (development, single-process): Events are published to an in-memory list of handlers within the same process. Delivery is synchronous or task-based within the process. There is zero network overhead, no configuration required, and sub-millisecond delivery. This is the default when no `EventBus__DefaultBackend` environment variable is set.

2. **Distributed Backends** (production, multi-process): Redis Streams, RabbitMQ, and Kafka implementations that provide:
   - Cross-process delivery via network protocols (Redis protocol, AMQP, Kafka binary protocol).
   - Durable storage so events survive broker restarts.
   - Consumer group semantics for load balancing across service instances.
   - At-least-once delivery guarantees through acknowledgment and offset commit.

The `IEventBus` interface abstracts the transport choice. Services depend on `IEventBus` and are agnostic to whether events are delivered in-memory or over the network. The `AdaptiveEventBus` or the `EventBus__DefaultBackend` environment variable selects the concrete implementation at startup.

## Consequences

**Benefits:**
- Developers can run the entire platform (all services) in a single process during development without installing Redis, RabbitMQ, or Kafka. This reduces onboarding time and eliminates "it works on my machine" issues.
- The same service code runs in development and production without changes. Only the configuration (environment variable) differs.
- Unit and integration tests use InMemory by default, making tests fast, deterministic, and independent of external infrastructure.
- Switching between backends requires no code changes, only configuration. This enables performance testing against different transports without modifying application logic.

**Trade-offs:**
- InMemory events are lost on process restart. If a developer restarts the TransactionService mid-flow, downstream events will not be delivered. This is acceptable in development but would be catastrophic in production.
- InMemory events do not propagate across service boundaries. When running services as separate local processes (the default local setup), each service has its own InMemory bus. Cross-service events only work when a distributed backend is configured.
- The distributed backends are currently stubs ready for full integration. Production deployments must validate that the chosen backend is fully implemented and tested.
- The abstraction layer adds indirection. Debugging event delivery issues requires understanding which backend is active and its specific failure modes.

**Risks:**
- A developer might inadvertently test against InMemory and assume the behavior applies to Redis or Kafka. CI/CD pipelines should include integration tests that run against a distributed backend to catch transport-specific issues early.
- The `IEventBus` abstraction may not expose backend-specific features (e.g., Kafka headers, RabbitMQ priorities). If these features become necessary, the abstraction will need to be extended or bypassed.
