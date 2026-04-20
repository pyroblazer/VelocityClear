# ADR-003: Redis Streams with XADD and XREADGROUP for Distributed Event Delivery

**Status:** Accepted
**Date:** 2026-04-22
**ISO 27001 Controls:** A.10.1.1 (Cryptographic controls), A.12.3.1 (Capacity management)
**ISO 9001 Clauses:** 7.5

## Context

When the adaptive event bus selects Redis as the transport backend (CPU utilization between 25% and 50%, or explicitly pinned via configuration), the platform needs a distributed event delivery mechanism that supports both fan-out (every service receives every event) and load balancing (multiple instances of a service share the event processing load).

Redis is already part of the infrastructure stack for caching and session management. Using Redis Streams avoids introducing an additional message broker while providing consumer group semantics that are essential for multi-instance service deployments.

## Decision

We use Redis Streams as the event transport with the following design:

- **Publishing:** Events are appended to a Redis Stream using `XADD` with the event type as the stream key and the serialized event payload as field-value pairs. Each `XADD` returns a unique timestamp-based entry ID that serves as the event's position in the stream.

- **Consuming:** Each service creates a consumer group using `XGROUP CREATE` with the service name as the group ID. Events are read using `XREADGROUP` with `GROUP <serviceName> <instanceId>`, which assigns each message to exactly one consumer within the group.

- **Acknowledgment:** After successful processing, the consumer calls `XACK` to confirm delivery. Unacknowledged messages remain in the Pending Entries List (PEL) and can be claimed by other instances if the original consumer crashes, providing at-least-once delivery semantics.

- **Fan-out:** Each service type creates its own consumer group on the same stream. Since Redis Streams deliver every message to every consumer group, all services receive all events. This matches the platform's requirement that RiskService, PaymentService, ComplianceService, and PinEncryptionService each receive every TransactionCreatedEvent.

## Consequences

**Benefits:**
- Consumer groups provide both fan-out (across services) and load balancing (within a service) using a single well-understood abstraction.
- The Pending Entries List enables crash recovery: if a service instance fails mid-processing, another instance can claim and reprocess the message.
- Redis Streams are an append-only log. `XRANGE` can replay events from any position, supporting debugging and event replay scenarios.
- Reuses the existing Redis infrastructure, avoiding operational overhead of an additional message broker.

**Trade-offs:**
- Redis Streams are persisted to memory (with optional disk persistence via RDB/AOF). In a hard crash without AOF, messages in transit may be lost. For a financial platform, the AOF persistence mode with `fsync everysec` should be configured in production.
- Redis Streams do not support partitioning natively within a single stream key. For very high throughput, the application would need to shard streams by key (e.g., `events:transactions:0`, `events:transactions:1`).
- Consumer group management (creating groups, handling `BUSYGROUP` errors) adds initialization complexity. The implementation handles this with `MKSTREAM` and idempotent group creation.
- Memory usage grows with stream length. A `MAXLEN` policy should be configured to trim old entries while preserving unacknowledged messages.

**Risks:**
- Redis is often perceived as a cache, not a message broker. Operations teams must treat Redis as a critical data store and configure persistence, replication, and backups accordingly.
