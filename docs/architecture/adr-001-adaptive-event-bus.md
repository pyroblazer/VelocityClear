# ADR-001: Adaptive Event Bus with CPU-Based Auto-Switching

**Status:** Accepted
**Date:** 2026-04-22
**ISO 27001 Controls:** A.12.1.2 (Change management), A.12.2.1 (Capacity management)
**ISO 9001 Clauses:** 7.5

## Context

A financial transaction platform must handle widely varying load levels. During off-peak hours, the system processes a handful of transactions per minute. During market open, flash sales, or fraud spikes, throughput requirements can increase by orders of magnitude. The event bus is the backbone of the platform, carrying TransactionCreated, RiskEvaluated, PaymentAuthorized, AuditLogged, and PinVerified events across all microservices.

Choosing a single messaging technology creates a tension: lightweight in-process messaging is fast and simple but cannot survive process restarts or scale across instances. Enterprise message brokers like Kafka provide durability and partition parallelism but introduce operational overhead that is wasteful at low load. We needed an architecture that adapts to current demand without manual intervention.

## Decision

We implemented an adaptive event bus (`AdaptiveEventBus`) that monitors CPU utilization and automatically switches between four backend implementations:

| CPU Range       | Backend        | Characteristics                                      |
|-----------------|----------------|------------------------------------------------------|
| < 25%           | InMemory       | Zero network overhead, sub-millisecond delivery       |
| 25% to < 50%    | Redis Streams  | Distributed, persistent, consumer group semantics     |
| 50% to < 75%    | RabbitMQ       | Durable queues, fanout exchanges, prefetch flow control|
| >= 75%          | Kafka          | High-throughput partitioned streaming, manual offsets  |

A pinning override (`EventBus__DefaultBackend` environment variable) allows operators to lock the platform to a specific backend in production, preventing unintended switching during sensitive operations. The adaptive switching is intended for development, staging, and environments where load varies unpredictably.

## Consequences

**Benefits:**
- Simplifies local development: developers run a single process with InMemory and get zero-config event delivery.
- Graduated operational complexity: teams can start with Redis and migrate to Kafka without changing application code.
- The pinning override ensures production environments run on a stable, tested backend.
- All backends implement the same `IEventBus` interface, so services are decoupled from the transport choice.

**Trade-offs:**
- The adaptive logic adds complexity to the event infrastructure layer.
- Switching backends at runtime requires draining in-flight messages to avoid event loss. The current implementation handles this by completing pending handlers before transitioning.
- CPU utilization is a proxy for load but is not perfect. Future iterations may incorporate message queue depth or transaction throughput as additional signals.
- Each backend requires its own operational expertise (Redis cluster management, RabbitMQ clustering, Kafka broker configuration).

**Risks:**
- If the pinning override is not set in production, an unexpected load spike could trigger a switch to a backend that has not been operationally validated. CI/CD pipelines should validate the pinning configuration before deployment.
