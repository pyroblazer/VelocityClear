# ADR-004: Kafka Topics Per Event Type with Consumer Groups for High-Throughput Streaming

**Status:** Accepted
**Date:** 2026-04-22
**ISO 27001 Controls:** A.12.1.2 (Change management), A.12.3.1 (Capacity management)
**ISO 9001 Clauses:** 7.5

## Context

At high load levels (CPU utilization >= 75%), the platform requires a messaging backbone capable of sustaining tens of thousands of events per second with strong durability guarantees. Financial transaction events must not be lost, must be ordered within a logical partition (e.g., per user or per transaction), and must be replayable for audit and recovery purposes.

Apache Kafka is the industry standard for high-throughput, durable event streaming. The question was how to map the platform's event types (TransactionCreated, RiskEvaluated, PaymentAuthorized, AuditLogged, PinVerified) to Kafka's topic and partition model, and how to configure consumer semantics to match the platform's reliability requirements.

## Decision

We use one Kafka topic per event type with the following configuration:

- **Topics:** Each event type maps to a dedicated topic (e.g., `transaction-created`, `risk-evaluated`, `payment-authorized`, `audit-logged`, `pin-verified`). This provides natural separation, allowing each topic to have its own retention policy, partition count, and replication factor.

- **Consumer Groups:** Each service instance joins a consumer group named after the service (e.g., `risk-service`, `payment-service`). Kafka's consumer group protocol assigns partitions to group members, providing automatic load balancing when instances are added or removed.

- **Offset Management:** Consumers commit offsets manually after successful event processing, ensuring at-least-once delivery semantics. Automatic offset commit is disabled to prevent marking a message as consumed before its side effects (database writes, downstream events) are persisted.

- **Producer Configuration:** `Acks=All` is set on the producer to ensure every write is confirmed by all in-sync replicas before being acknowledged. This prevents data loss if a broker fails.

- **Partitioning Strategy:** Events are partitioned by transaction ID, ensuring that all events for a single transaction (created, risk evaluated, payment authorized, audit logged) land on the same partition and are consumed in order.

## Consequences

**Benefits:**
- Topics per event type allow independent scaling. The `transaction-created` topic can have 12 partitions for high throughput while `pin-verified` can have 3 partitions for lower volume.
- Consumer groups enable horizontal scaling: adding a new PaymentService instance automatically redistributes partitions across the group.
- Manual offset commit ensures no message is marked consumed before its side effects are durable, preventing data loss during failures.
- `Acks=All` combined with a replication factor of 3 provides durability against single-broker failures.
- Partitioning by transaction ID guarantees ordering within a transaction's lifecycle, which is critical for the RiskService and PaymentService to process events in the correct sequence.

**Trade-offs:**
- Kafka is operationally complex. A production deployment requires ZooKeeper or KRaft, broker configuration, topic management, and monitoring of consumer lag.
- Manual offset commit means duplicate processing is possible after a crash. All event handlers must be idempotent.
- Partition count is fixed at topic creation and cannot be changed without recreating the topic. Over-provisioning partitions wastes resources; under-provisioning limits parallelism.
- At-least-once semantics require downstream services to handle duplicate events gracefully.

**Risks:**
- If consumer lag grows faster than processing capacity, the platform falls behind. Monitoring consumer lag with alerts is essential.
- Kafka's disk-based storage is durable but introduces higher latency compared to in-memory alternatives. This is acceptable for the high-load scenario where throughput matters more than sub-millisecond latency.
