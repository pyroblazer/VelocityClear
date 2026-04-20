# ADR-005: RabbitMQ Fanout Exchange with Per-Service Queues for Message Broadcasting

**Status:** Accepted
**Date:** 2026-04-22
**ISO 27001 Controls:** A.12.1.2 (Change management), A.12.2.1 (Capacity management)
**ISO 9001 Clauses:** 7.5

## Context

At moderate load levels (CPU utilization between 50% and 75%), the platform needs reliable message broadcasting across services. Events such as TransactionCreated must reach RiskService, PaymentService, ComplianceService, and PinEncryptionService simultaneously. The messaging system must survive broker restarts, handle processing failures gracefully, and provide flow control to prevent fast producers from overwhelming slow consumers.

RabbitMQ is a mature, battle-tested message broker that supports various exchange types, message durability, acknowledgment semantics, and prefetch-based flow control. The question was how to configure RabbitMQ's exchange and queue topology to match the platform's fan-out requirements.

## Decision

We use a durable fanout exchange with per-service queues, configured as follows:

- **Exchange:** A single durable fanout exchange is created for all platform events. A fanout exchange broadcasts every published message to all bound queues unconditionally, which matches the requirement that every service receives every event.

- **Queues:** Each service binds its own durable queue to the fanout exchange. Queue names follow the convention `<service-name>.events` (e.g., `risk-service.events`, `payment-service.events`). Durable queues survive broker restarts because their metadata is persisted to disk.

- **Failure Handling:** When a message handler throws an exception, the consumer calls `BasicNack` with `requeue: true`, which returns the message to the head of the queue for immediate retry. This provides implicit retry without requiring a separate retry mechanism.

- **Flow Control:** A prefetch count of 10 is set via `BasicQos`. This limits the number of unacknowledged messages the broker will push to each consumer, preventing a fast producer from overwhelming a slow consumer with more messages than it can process.

- **Acknowledgment:** Successful processing calls `BasicAck` to confirm delivery. The broker removes the message from the queue only after acknowledgment.

## Consequences

**Benefits:**
- Fanout exchange provides natural fan-out: every bound queue receives every message without routing logic or topic matching. Adding a new service simply requires binding a new queue.
- Durable queues ensure messages survive broker restarts. Combined with persistent message delivery mode (`delivery_mode: 2`), messages are written to disk before being acknowledged by the broker.
- Prefetch-based flow control creates a natural backpressure mechanism. If a consumer falls behind, the broker stops sending messages until earlier ones are acknowledged, protecting the consumer from overload.
- RabbitMQ's management UI (port 15672) provides visibility into queue depths, consumer counts, message rates, and error rates, simplifying operational monitoring.

**Trade-offs:**
- `BasicNack` with requeue provides implicit retry but does not implement retry delays or max-retry counts. A poison message (one that always fails) will be requeued indefinitely, blocking subsequent messages. A dead letter exchange should be configured in production to route messages that exceed a retry threshold.
- Fanout exchanges do not support message filtering. Every queue receives every event. Services that only need a subset of events must filter in their handler code. Topic exchanges could provide routing-key-based filtering but add configuration complexity.
- RabbitMQ is a traditional message broker, not a log. Once a message is acknowledged and consumed, it is removed from the queue. There is no built-in replay capability. For event replay, consider routing a copy to a durable audit queue.

**Risks:**
- The requeue-without-limit pattern can cause message storms if a systematic failure causes all messages to be requeued. A dead letter queue with TTL-based expiration should be added before production deployment.
- Queue depth must be monitored. If a service falls behind, its queue grows unbounded, consuming broker memory and disk.
