// ============================================================================
// AuditService.cs - Audit Logging with Cryptographic Hash Chaining
// ============================================================================
// This service creates immutable audit log entries by chaining them with
// SHA-256 cryptographic hashes. Each audit log's hash is computed from its
// payload combined with the previous log's hash - forming a chain similar to
// a blockchain. Any tampering with a log entry would break the chain and be
// detectable via the /api/audit/verify endpoint.
//
// Key concepts:
//   - SHA256.HashData(): Computes a SHA-256 cryptographic hash from byte data.
//     Produces a 256-bit (32-byte) hash that is deterministic and one-way.
//   - Convert.ToHexString(): Converts a byte array to a uppercase hexadecimal
//     string (e.g., [0xAB, 0xCD] -> "ABCD").
//   - JsonSerializer.Serialize(): Converts a C# object to a JSON string.
//   - FirstOrDefaultAsync(): Returns the first matching entity or null.
//   - OrderByDescending(): LINQ operator for descending sort order.
// ============================================================================

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinancialPlatform.ComplianceService;
using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Services;

public class AuditService
{
    private readonly ComplianceDbContext _dbContext;
    private readonly ISseHub _sseHub;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ComplianceDbContext dbContext,
        ISseHub sseHub,
        ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _sseHub = sseHub;
        _logger = logger;
    }

    public async Task LogEventAsync(string eventType, object payload)
    {
        // JsonSerializer.Serialize() converts any C# object to a JSON string.
        // This stores the full event data as a string in the database, allowing
        // different event types with different structures to be stored uniformly.
        var payloadJson = JsonSerializer.Serialize(payload);

        // Get the hash of the most recent audit log to chain to it.
        var previousHash = await GetLastHashAsync();

        // Compute a SHA-256 hash of this log's payload combined with the
        // previous log's hash. This creates the chain - any change to this
        // payload or any earlier payload would produce a different hash.
        var hash = ComputeHash(payloadJson, previousHash);

        // Create the audit log entity with all required fields.
        var auditLog = new Shared.Models.AuditLog
        {
            // Guid.NewGuid() generates a unique ID for this audit entry.
            Id = Guid.NewGuid().ToString(),
            EventType = eventType,
            Payload = payloadJson,
            Hash = hash,
            PreviousHash = previousHash,
            CreatedAt = DateTime.UtcNow
        };

        // Add() tracks the entity for insertion. SaveChangesAsync() executes
        // the SQL INSERT statement.
        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();

        ServiceMetrics.AuditLogsCreatedTotal.WithLabels(eventType).Inc();

        _logger.LogInformation("Audit log created: {EventType} for {AuditId}", eventType, auditLog.Id);

        // Broadcast the new audit log to all connected SSE clients for real-time
        // monitoring in the frontend dashboards.
        await _sseHub.BroadcastAsync("AuditLogged", new
        {
            auditLog.Id,
            auditLog.EventType,
            auditLog.Hash,
            auditLog.CreatedAt
        });
    }

    // "string?" - the return type is nullable. Returns null if no audit logs exist.
    private async Task<string?> GetLastHashAsync()
    {
        // OrderByDescending(a => a.CreatedAt) sorts by newest first.
        // FirstOrDefaultAsync() returns the first result or null if the table is empty.
        // Together these get the most recent audit log (or null if none exist).
        var lastLog = await _dbContext.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        // The "?." (null-conditional operator) safely accesses Hash only if
        // lastLog is not null. If lastLog is null, the entire expression is null.
        return lastLog?.Hash;
    }

    // "private static" - this method is private (only accessible within this class)
    // and static (doesn't access instance fields - it's a pure function).
    public static string ComputeHash(string payload, string? previousHash)
    {
        // Combine the payload with the previous hash (or empty string if none).
        // This ensures each hash depends on all previous entries in the chain.
        var combined = payload + (previousHash ?? string.Empty);

        // SHA256.HashData() computes the SHA-256 hash of a byte array.
        // This is a static method (called on the class, not an instance).
        // It returns a 32-byte array (256 bits).
        //
        // Encoding.UTF8.GetBytes() converts the string to a UTF-8 byte array,
        // which is what cryptographic functions operate on.
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));

        // Convert.ToHexString() converts a byte array to an uppercase hexadecimal
        // string representation. E.g., [0x4A, 0x1B] becomes "4A1B".
        // This produces a 64-character string (32 bytes * 2 hex chars per byte).
        return Convert.ToHexString(bytes);
    }
}
