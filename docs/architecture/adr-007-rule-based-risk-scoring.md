# ADR-007: Rule-Based Risk Scoring Engine for Real-Time Fraud Detection

**Status:** Accepted
**Date:** 2026-04-22
**ISO 27001 Controls:** A.12.6.1 (Management of technical vulnerabilities), A.16.1.1 (Events management)
**ISO 9001 Clauses:** 7.5

## Context

Every financial transaction must be assessed for fraud risk in real time before payment authorization. The risk score determines whether the transaction is approved, flagged for review, or rejected. The assessment must be fast (sub-100 milliseconds), deterministic (same inputs always produce the same score), and auditable (regulators must be able to understand why a transaction was flagged or approved).

Two approaches were considered: machine learning models (trained on historical transaction data) and rule-based scoring (explicit, human-readable rules). While ML models can capture complex patterns, they require training data, model versioning, feature stores, and explainability tooling. For a platform that needs deterministic, auditable decisions from day one, rule-based scoring is the appropriate starting point.

## Decision

We implemented a rule-based scoring engine that evaluates four independent risk factors and sums their contributions to produce a score from 0 to 100:

| Factor         | Condition                         | Score Addition |
|----------------|-----------------------------------|----------------|
| Amount         | Transaction amount > $5,000       | +30            |
| Amount         | Transaction amount > $10,000      | +20 additional |
| Velocity       | > 5 transactions/min per user     | +25            |
| Time-of-day    | Between 10:00 PM and 6:00 AM      | +15            |

The resulting score is classified into three risk levels:

| Level  | Score Range |
|--------|-------------|
| HIGH   | >= 80       |
| MEDIUM | >= 50       |
| LOW    | < 50        |

The scoring engine is implemented in the RiskService as a synchronous, stateless computation (except for velocity tracking, which uses an in-memory sliding window per user). The risk score is published as a RiskEvaluated event and consumed by the PaymentService for authorization decisions.

## Consequences

**Benefits:**
- Rules are deterministic and auditable. Given the same transaction amount, velocity, and timestamp, the score is always the same. This satisfies regulatory requirements for explainable decisions.
- Rules are tunable without model retraining. If the fraud team identifies a new pattern, a rule can be added or thresholds adjusted without data science involvement.
- Amount thresholds align with AML (Anti-Money Laundering) reporting requirements. Transactions above $10,000 trigger enhanced due diligence in many jurisdictions, and the +50 combined amount score pushes these into HIGH or MEDIUM risk.
- Velocity detection (>5 transactions per minute per user) catches card testing attacks where fraudsters submit many small transactions to validate stolen card numbers.
- Time-of-day scoring reflects elevated risk during off-hours (10 PM to 6 AM), when legitimate financial activity is less common and fraud attempts are more likely.
- The additive scoring model is simple to understand: each factor contributes independently, and the total score is the sum. New factors can be added without modifying the existing rules.

**Trade-offs:**
- Rule-based scoring cannot capture complex, non-linear fraud patterns that ML models can detect (e.g., subtle combinations of amount, merchant category, and geolocation). As the platform matures, ML models should supplement (not replace) the rule engine.
- The velocity tracking is in-memory and per-instance. If RiskService runs multiple instances, each tracks velocity independently, allowing a user to exceed 5 transactions/minute across instances without detection. This should be backed by Redis in production.
- Fixed thresholds ($5K, $10K, 5 txns/min) may not be optimal for all transaction types or currencies. The scoring engine should be made configurable to support different thresholds per transaction category.
- The maximum possible score is 90 (30 + 20 + 25 + 15), so HIGH risk requires at least two factors to trigger.

**Risks:**
- Sophisticated attackers can reverse-engineer the rules and structure transactions to stay below thresholds (e.g., $4,999 amounts, 5 transactions per minute). Anomaly detection ML models should be layered on top to catch adversarial behavior.
