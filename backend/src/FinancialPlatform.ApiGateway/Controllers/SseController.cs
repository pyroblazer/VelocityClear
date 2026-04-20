// ============================================================================
// SseController.cs - Server-Sent Events (SSE) Streaming Endpoint
// ============================================================================
// This controller manages real-time event streaming to clients using the SSE
// protocol. SSE is a lightweight, unidirectional (server-to-client) protocol
// built on top of HTTP. Unlike WebSockets, SSE:
//   - Uses a standard HTTP connection (no upgrade handshake)
//   - Is server-to-client only (clients cannot send data back on the same stream)
//   - Automatically reconnects on disconnection (built into the browser API)
//   - Works through most firewalls and proxies since it is plain HTTP
//
// Endpoint: GET /api/sse/stream
// The client opens this URL and holds the connection open. The server pushes
// events as they occur. The ISseHub manages all active connections and
// broadcasts events to every connected client.
// ============================================================================

using FinancialPlatform.ApiGateway;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.ApiGateway.Controllers;

[Authorize]
[ApiController]
public class SseController : ControllerBase
{
    // ISseHub - interface for managing SSE client connections. The actual
    // implementation (InMemorySseHub) maintains a dictionary of active
    // connections and provides methods to broadcast events to all clients.
    private readonly ISseHub _sseHub;

    // Constructor injection - ASP.NET provides the ISseHub implementation
    // that was registered as a singleton in Program.cs.
    public SseController(ISseHub sseHub)
    {
        _sseHub = sseHub;
    }

    // --------------------------------------------------------------------------
    // [HttpGet("/api/sse/stream")] - Maps to GET /api/sse/stream.
    //   Note the leading "/" - this is an absolute route override, meaning
    //   it ignores any [Route] attribute on the class (if one existed).
    //
    // "async Task" - This is an asynchronous method that returns a Task
    //   (a promise/future representing ongoing work). "async" enables the
    //   use of "await" inside the method. In JavaScript terms, this is
    //   similar to an async function that returns Promise<void>.
    //
    // CancellationToken - ASP.NET automatically provides this token, which
    //   is triggered when the client disconnects or the request is aborted.
    //   It allows long-running operations to be cancelled cleanly.
    // --------------------------------------------------------------------------
    [HttpGet("/api/sse/stream")]
    public async Task Stream(CancellationToken ct)
    {
        // Set up the HTTP response for SSE protocol compliance:
        //   Content-Type: "text/event-stream" - required MIME type for SSE
        //   Cache-Control: "no-cache" - prevents proxy/browser caching
        //   Connection: "keep-alive" - tells intermediaries to keep the TCP connection open
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        // --------------------------------------------------------------------------
        // Guid.NewGuid() generates a cryptographically random UUID (v4).
        // .ToString() formats it as a hyphenated lowercase string.
        // This unique ID identifies this particular SSE client connection.
        // --------------------------------------------------------------------------
        var clientId = Guid.NewGuid().ToString();

        // Register this client's HTTP Response stream with the SSE hub.
        // The hub stores the response so it can write events to this client later.
        _sseHub.AddClient(clientId, Response);
        ServiceMetrics.SseActiveConnections.Set(_sseHub.ActiveConnections);

        try
        {
            // --------------------------------------------------------------------------
            // This loop keeps the HTTP connection alive. The server sits in this loop
            // until the client disconnects (triggering the CancellationToken).
            //
            // ct.IsCancellationRequested - checks if cancellation has been signaled.
            // The "!" operator negates the boolean, so the loop continues while
            // cancellation has NOT been requested.
            //
            // Task.Delay(1000, ct) - asynchronously waits 1000ms (1 second) without
            // blocking a thread. The CancellationToken allows the delay to be
            // interrupted early if the client disconnects. This is like
            // await new Promise(resolve => setTimeout(resolve, 1000)) in JS,
            // but with cancellation support.
            // --------------------------------------------------------------------------
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);
            }
        }
        finally
        {
            // The "finally" block runs regardless of how the try block exits -
            // whether normally, via exception, or via cancellation. This ensures
            // the client is always removed from the hub, preventing memory leaks.
            _sseHub.RemoveClient(clientId);
            ServiceMetrics.SseActiveConnections.Set(_sseHub.ActiveConnections);
        }
    }
}
