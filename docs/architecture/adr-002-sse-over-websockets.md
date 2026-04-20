# ADR-002: Server-Sent Events over WebSockets for Real-Time Dashboard Updates

**Status:** Accepted
**Date:** 2026-04-22
**ISO 27001 Controls:** A.13.1.1 (Network controls), A.14.1.2 (Securing application services)
**ISO 9001 Clauses:** 7.5

## Context

The financial transaction platform includes multiple browser-based dashboards: transaction monitoring, risk assessment, audit trails, and card operations. Operators need real-time visibility into transaction events as they flow through the system (TransactionCreated, RiskEvaluated, PaymentAuthorized, AuditLogged, PinVerified). The question was whether to use WebSockets (full-duplex) or Server-Sent Events (SSE) for pushing updates to connected browsers.

The platform's event flow is inherently unidirectional from the server's perspective: services publish events, and the dashboards consume them for display. The dashboards do not send data back through the real-time channel; they use standard REST API calls for any client-to-server communication (creating transactions, querying history, etc.).

## Decision

We chose Server-Sent Events (SSE) over WebSockets for the real-time event push channel. The implementation uses an `ISseHub` interface with an `InMemorySseHub` concrete class that maintains a thread-safe list of connected clients and broadcasts serialized event payloads to all subscribers.

The API Gateway exposes a single SSE endpoint at `GET /api/sse/stream` that:
1. Sets `Content-Type: text/event-stream` and disables response buffering.
2. Keeps the HTTP connection open indefinitely.
3. Sends each event as an SSE-formatted message (`data: {json}\n\n`).
4. Handles client disconnections gracefully by removing the client from the subscriber list.

Frontend dashboards connect using the browser-native `EventSource` API, which provides automatic reconnection with the `Last-Event-ID` header for resuming from the last received event.

## Consequences

**Benefits:**
- SSE is HTTP-native: it works through corporate proxies, firewalls, and load balancers without special configuration. WebSockets require a protocol upgrade handshake that some intermediaries block.
- The `EventSource` API provides built-in auto-reconnection. With WebSockets, reconnection logic must be implemented manually.
- Simpler server implementation: SSE uses standard ASP.NET Core `HttpResponse` writing, no need for WebSocket middleware, connection state machines, or frame parsing.
- Naturally matches our unidirectional event flow. The dashboards only need to receive events, not send them over the same channel.
- Each event is a standard HTTP response chunk, so HTTP/2 multiplexing and caching headers work as expected.

**Trade-offs:**
- SSE is unidirectional (server-to-client only). If we later need bidirectional communication (e.g., dashboards pushing commands back through the event channel), we would need to add a separate REST endpoint or migrate to WebSockets.
- SSE has a maximum connection limit of 6 per browser origin under HTTP/1.1 (HTTP/2 removes this limit). Our dashboards each open one connection, so this is not a constraint today.
- Binary data must be base64-encoded in SSE messages. Our events are JSON, so this is not relevant.
- No built-in binary framing. Each message is UTF-8 text delimited by newlines. Acceptable for JSON event payloads.

**Risks:**
- Long-lived HTTP connections can be dropped by aggressive proxy timeouts. Heartbeat messages (empty SSE comments) should be added in production to keep connections alive.
