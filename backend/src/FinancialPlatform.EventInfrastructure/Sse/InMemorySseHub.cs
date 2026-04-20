/*
 * InMemorySseHub.cs
 *
 * PURPOSE:
 * Implements Server-Sent Events (SSE) broadcasting to connected clients.
 * SSE is a one-way real-time communication protocol where the server pushes
 * updates to the browser over a long-lived HTTP connection. Unlike WebSockets,
 * SSE is simpler and works over standard HTTP - the client opens a connection
 * and the server sends "data: ..." text frames.
 *
 * This hub maintains a dictionary of all connected clients (each represented
 * by their HTTP response stream) and broadcasts event messages to all of them.
 * Clients that fail to receive messages are automatically cleaned up.
 *
 * KEY C# CONCEPTS USED:
 *   - ConcurrentDictionary for thread-safe concurrent access
 *   - async/await for non-blocking I/O operations
 *   - Deconstruction in foreach (var (key, value) in dict)
 *   - Anonymous types (new { ... }) for ad-hoc object creation
 *   - String interpolation ($"...")
 *   - JSON serialization with System.Text.Json
 *   - HttpResponse streaming for real-time data push
 */

// System.Collections.Concurrent provides thread-safe collection types.
// Unlike regular Dictionary, ConcurrentDictionary allows multiple threads
// to read and write simultaneously without explicit locking.
using System.Collections.Concurrent;

// System.Text.Json is the built-in JSON serialization library in .NET.
// JsonSerializer.Serialize converts C# objects to JSON strings.
using System.Text.Json;

// ISseHub defines the contract for SSE hub implementations.
using FinancialPlatform.Shared.Interfaces;

// HttpResponse represents the HTTP response being sent back to the client.
// In SSE, we keep this response open and write data frames to it over time.
using Microsoft.AspNetCore.Http;

// ILogger<T> provides structured logging, with T as the category name.
using Microsoft.Extensions.Logging;

namespace FinancialPlatform.EventInfrastructure.Sse;

// ISseHub is the interface for all SSE hub implementations. This in-memory
// version stores clients in a dictionary within the process. A distributed
// version might use Redis pub/sub to broadcast across multiple server instances.
public class InMemorySseHub : ISseHub
{
    // ConcurrentDictionary is a thread-safe dictionary. Unlike regular
    // Dictionary<TKey, TValue>, it handles concurrent reads and writes from
    // multiple threads without requiring explicit lock statements.
    //   - Key:   string (a unique client identifier, often a GUID)
    //   - Value: HttpResponse (the open HTTP connection to that client)
    // Using ConcurrentDictionary here is essential because clients can connect
    // and disconnect on different threads simultaneously while broadcasts
    // iterate over all entries.
    private readonly ConcurrentDictionary<string, HttpResponse> _clients = new();

    // Logger for this class, following the underscore-prefix convention for
    // private fields and using the class type as the logging category.
    private readonly ILogger<InMemorySseHub> _logger;

    // A property with only a getter (expression-bodied). This exposes the
    // current number of connected clients. The "=>" syntax means the property
    // body is a single expression - equivalent to writing:
    //   public int ActiveConnections { get { return _clients.Count; } }
    public int ActiveConnections => _clients.Count;

    public InMemorySseHub(ILogger<InMemorySseHub> logger)
    {
        _logger = logger;
    }

    // Adds a new SSE client. The response parameter is the open HTTP response
    // that was established when the client requested the SSE endpoint. By
    // storing it, we can write to it later during BroadcastAsync.
    public void AddClient(string clientId, HttpResponse response)
    {
        // The indexer _clients[clientId] = response adds or updates the entry.
        // ConcurrentDictionary's indexer is thread-safe internally.
        _clients[clientId] = response;

        // Structured logging: {ClientId} and {Count} are placeholders that the
        // logging framework replaces with the provided values. This is more
        // efficient than string concatenation because the template is cached.
        _logger.LogInformation("SSE client {ClientId} connected. Total: {Count}", clientId, _clients.Count);
    }

    public void RemoveClient(string clientId)
    {
        // TryRemove safely removes an entry. The "out _" syntax discards the
        // removed value - the underscore is the discard variable, meaning
        // "I don't care about the removed value." This avoids a compiler
        // warning about an unused variable.
        _clients.TryRemove(clientId, out _);
        _logger.LogInformation("SSE client {ClientId} disconnected. Total: {Count}", clientId, _clients.Count);
    }

    // BroadcastAsync sends a message to every connected client.
    // "async Task" makes this an asynchronous method returning a Task.
    // The <T> generic parameter allows any data type to be broadcast.
    public async Task BroadcastAsync<T>(string eventType, T data)
    {
        // JsonSerializer.Serialize converts the C# object to a JSON string.
        // The "new { ... }" syntax creates an anonymous type - a class with
        // no explicit name, generated by the compiler. It's useful for
        // creating ad-hoc data structures without defining a formal class.
        // The property names (type, data, timestamp) become JSON keys.
        var payload = JsonSerializer.Serialize(new
        {
            type = eventType,
            data,
            // DateTime.UtcNow returns the current UTC time. UTC is preferred
            // over local time in server applications to avoid timezone issues.
            // (DateTimeOffset would include timezone offset info - DateTime
            // does not.)
            timestamp = DateTime.UtcNow
        });

        // Track clients that failed to receive the message so we can remove
        // them after the loop (modifying a collection while iterating it
        // can cause exceptions).
        var disconnected = new List<string>();

        // Deconstruction in foreach: "var (clientId, response)" extracts
        // the key and value from each KeyValuePair in the dictionary.
        // This is equivalent to:
        //   foreach (var kvp in _clients)
        //   {
        //       var clientId = kvp.Key;
        //       var response = kvp.Value;
        //   }
        foreach (var (clientId, response) in _clients)
        {
            try
            {
                // SSE format requires "data: " prefix followed by the message
                // content, terminated with two newlines (\n\n). This is the
                // standard SSE wire format defined by the W3C specification.
                // The $"..." is string interpolation - expressions in {} are
                // evaluated and inserted into the string.
                await response.WriteAsync($"data: {payload}\n\n");

                // FlushAsync forces any buffered data to be written to the
                // network stream immediately. Without flushing, data may sit
                // in a buffer and not reach the client until the buffer fills
                // or the response ends - defeating the purpose of real-time push.
                await response.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                // Exceptions typically occur when the client has disconnected
                // (broken pipe, network failure, browser tab closed). We log
                // a warning rather than an error since this is an expected scenario.
                _logger.LogWarning(ex, "Failed to send SSE to client {ClientId}", clientId);
                disconnected.Add(clientId);
            }
        }

        // Remove disconnected clients outside the iteration loop to avoid
        // "Collection was modified" exceptions that would occur if we tried
        // to remove entries while iterating the dictionary.
        foreach (var clientId in disconnected)
        {
            // "out _" discards the removed HttpResponse. We don't need it
            // because the connection is already dead.
            _clients.TryRemove(clientId, out _);
        }
    }
}
