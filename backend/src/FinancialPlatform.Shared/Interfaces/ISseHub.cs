// ============================================================================
// ISseHub.cs - Server-Sent Events (SSE) Hub Interface
//
// This file defines the contract for an SSE hub. SSE is a technology that allows
// the server to push real-time updates to connected clients over HTTP - the client
// opens a long-lived connection and the server sends events as they happen.
// Unlike WebSockets, SSE is one-directional (server-to-client only) and uses
// standard HTTP, making it simpler to implement and more firewall-friendly.
//
// In this platform, the SSE hub broadcasts transaction lifecycle events (created,
// risk-evaluated, payment-authorized, audit-logged) to all connected frontends.
// ============================================================================

// HttpResponse is the ASP.NET Core type representing an outgoing HTTP response.
// We need it here because SSE works by holding open an HTTP response stream.
using Microsoft.AspNetCore.Http;

namespace FinancialPlatform.Shared.Interfaces;

public interface ISseHub
{
    // Broadcasts an event of the given type to ALL currently connected clients.
    // The generic <T> allows any data type to be sent as the payload.
    // "string eventType" is a label (e.g., "TransactionCreated") so clients can
    // distinguish between different event types.
    Task BroadcastAsync<T>(string eventType, T data);

    // "void" means this method returns nothing (synchronous, no async).
    // Registers a new SSE client by storing their response stream so the server
    // can write to it later. HttpResponse represents the open HTTP connection
    // to that specific client.
    void AddClient(string clientId, HttpResponse response);

    // Removes a client from the hub when they disconnect, cleaning up resources.
    void RemoveClient(string clientId);

    // This is a property declaration inside an interface. "{ get; }" means any
    // implementing class must provide a read-only property that returns the number
    // of currently active SSE connections. In C#, properties are a first-class
    // concept - they look like fields but can have custom get/set logic.
    int ActiveConnections { get; }
}
