// ============================================================================
// AuditController.cs - HTTP API Controller for Audit Log Operations
// ============================================================================
// This controller exposes endpoints for querying audit logs, verifying the
// integrity of the hash chain, and retrieving audit statistics. The hash chain
// verification is the key compliance feature - it detects any tampering with
// audit log records by checking that each log's hash is derived from its
// predecessor's hash.
//
// Key concepts:
//   - [FromQuery]: Binds parameters from the URL query string (e.g., ?page=1).
//   - LINQ: OrderByDescending(), Skip(), Take(), GroupBy(), Select(), CountAsync(),
//     ToListAsync() - query operators translated to SQL by EF Core.
//   - Pagination: The Skip/Take pattern for returning subsets of large result sets.
//   - Anonymous types: Used for ad-hoc JSON response shapes without defining classes.
//   - "is not null": Pattern matching for null checks (preferred C# idiom).
// ============================================================================

using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.ComplianceService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Controllers;

[Authorize]
[ApiController]
// [controller] token substitution: "AuditController" becomes "audit" (lowercase).
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly ComplianceDbContext _dbContext;
    private readonly ILogger<AuditController> _logger;

    public AuditController(ComplianceDbContext dbContext, ILogger<AuditController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// List all audit logs (paginated).
    /// </summary>
    [HttpGet]
    // [FromQuery] tells the model binder to read "page" and "pageSize" from the
    // URL query string (e.g., GET /api/audit?page=2&pageSize=10). Default values
    // are used when the query parameters are not provided.
    public async Task<IActionResult> GetAuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        // Input sanitization: ensure pagination values are reasonable.
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // CountAsync() executes a SQL COUNT query to get the total number of rows.
        // This is needed to calculate total pages for the pagination response.
        var total = await _dbContext.AuditLogs.CountAsync();

        // Pagination using Skip() and Take():
        //   - OrderByDescending(a => a.CreatedAt) - sort by newest first.
        //   - Skip((page - 1) * pageSize) - skip rows from previous pages.
        //   - Take(pageSize) - return only the current page's rows.
        //   - ToListAsync() - execute the query and materialize results.
        var logs = await _dbContext.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            Data = logs,
            Page = page,
            PageSize = pageSize,
            Total = total,
            // Math.Ceiling() rounds up to ensure partial pages count as a full page.
            // The (double) cast is needed because integer division truncates.
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    /// <summary>
    /// Get a specific audit log by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAuditLog(string id)
    {
        // FindAsync(id) looks up an entity by primary key. It checks the local
        // change tracker first (fast) before querying the database.
        var log = await _dbContext.AuditLogs.FindAsync(id);

        // "is null" is C# pattern matching syntax for null checking.
        if (log is null)
        {
            return NotFound(new { Message = $"Audit log '{id}' not found." });
        }

        return Ok(log);
    }

    /// <summary>
    /// Verify hash chain integrity across all audit logs.
    /// </summary>
    [HttpGet("verify")]
    public async Task<IActionResult> VerifyChain()
    {
        // Load all logs ordered by creation time (oldest first) to verify
        // the hash chain from beginning to end.
        var logs = await _dbContext.AuditLogs
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        if (logs.Count == 0)
        {
            return Ok(new { Valid = true, CheckedCount = 0, Message = "No audit logs to verify." });
        }

        var brokenLinks = new List<object>();

        // "string?" means the variable can hold null. The initial value is null.
        string? expectedPreviousHash = null;

        // "for" loop with "var" - the compiler infers the type as int.
        for (var i = 0; i < logs.Count; i++)
        {
            var log = logs[i];

            // The first log in the chain should have no previous hash reference.
            if (i == 0)
            {
                // "is not null" is C# pattern matching for "not null" - it's the
                // inverse of "is null" and is preferred over "!= null".
                if (log.PreviousHash is not null)
                {
                    brokenLinks.Add(new
                    {
                        LogId = log.Id,
                        Index = i,
                        Issue = "First log has a non-null PreviousHash"
                    });
                }

                // Recompute the hash from payload and verify it matches the stored hash
                var recomputed = AuditService.ComputeHash(log.Payload, null);
                if (log.Hash != recomputed)
                {
                    brokenLinks.Add(new
                    {
                        LogId = log.Id,
                        Index = i,
                        Issue = $"Hash mismatch: stored '{log.Hash}', recomputed '{recomputed}'"
                    });
                }

                expectedPreviousHash = log.Hash;
                continue;  // Skip to the next iteration
            }

            // Each subsequent log's PreviousHash must match the prior log's Hash.
            // If they don't match, the chain has been tampered with or corrupted.
            if (log.PreviousHash != expectedPreviousHash)
            {
                brokenLinks.Add(new
                {
                    LogId = log.Id,
                    Index = i,
                    // $"..." is string interpolation - expressions in {} are evaluated.
                    Issue = $"PreviousHash mismatch: expected '{expectedPreviousHash}', found '{log.PreviousHash}'"
                });
            }

            // Recompute the hash from payload + previousHash and verify it matches
            var recomputedHash = AuditService.ComputeHash(log.Payload, expectedPreviousHash);
            if (log.Hash != recomputedHash)
            {
                brokenLinks.Add(new
                {
                    LogId = log.Id,
                    Index = i,
                    Issue = $"Hash mismatch: stored '{log.Hash}', recomputed '{recomputedHash}'"
                });
            }

            expectedPreviousHash = log.Hash;
        }

        var isValid = brokenLinks.Count == 0;

        _logger.LogInformation("Hash chain verification: {Status}. Checked {Count} logs, {Broken} broken links.",
            isValid ? "VALID" : "INVALID", logs.Count, brokenLinks.Count);

        return Ok(new
        {
            Valid = isValid,
            CheckedCount = logs.Count,
            BrokenLinks = brokenLinks,
            // Ternary operator: condition ? valueIfTrue : valueIfFalse
            Message = isValid
                ? "Hash chain integrity verified successfully."
                : $"Hash chain integrity check failed. {brokenLinks.Count} broken link(s) found."
        });
    }

    /// <summary>
    /// Get audit statistics (count by event type).
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalCount = await _dbContext.AuditLogs.CountAsync();

        // GroupBy() groups rows by EventType, then Select() projects each group
        // into a summary object with the key (EventType) and count.
        // In SQL: SELECT EventType, COUNT(*) FROM AuditLogs GROUP BY EventType
        var byEventType = await _dbContext.AuditLogs
            .GroupBy(a => a.EventType)
            .Select(g => new { EventType = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        // FirstOrDefaultAsync() returns the first matching row or null if none exist.
        // Used here to find the earliest and latest audit log entries.
        var earliest = await _dbContext.AuditLogs.OrderBy(a => a.CreatedAt).FirstOrDefaultAsync();
        var latest = await _dbContext.AuditLogs.OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync();

        return Ok(new
        {
            TotalCount = totalCount,
            ByEventType = byEventType,
            // Conditional expression with member access: "earliest is not null ? ... : null"
            // The "earliest.Id" syntax works because C# knows "earliest" is not null
            // in the true branch of the ternary, so it allows direct member access.
            EarliestLog = earliest is not null ? new { earliest.Id, earliest.CreatedAt } : null,
            LatestLog = latest is not null ? new { latest.Id, latest.CreatedAt } : null
        });
    }
}
