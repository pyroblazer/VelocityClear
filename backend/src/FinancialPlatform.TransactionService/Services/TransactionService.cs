// ============================================================================
// TransactionService.cs - Business Logic for Transaction Operations
// ============================================================================
// This service encapsulates the business logic for creating and querying
// transactions. It coordinates between the database (via DbContext) and the
// event bus (for publishing events when transactions are created).
//
// Key concepts:
//   - Async/await: Methods returning Task<T> are asynchronous - they don't block
//     threads while waiting for I/O (database, network).
//   - LINQ: Language Integrated Query - methods like Where(), OrderByDescending(),
//     Select(), ToListAsync() that provide a SQL-like query syntax in C#.
//   - nameof(): Returns the name of a variable, type, or member as a string.
//     Refactoring-safe - if you rename the parameter, the string updates too.
//   - Structured logging: Using {PropertyName} placeholders instead of string
//     interpolation for machine-parseable log output.
// ============================================================================

using FinancialPlatform.TransactionService;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;
using FinancialPlatform.Shared.Models;
using FinancialPlatform.TransactionService.Data;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.TransactionService.Services;

public class TransactionService
{
    // These fields are populated via constructor injection. The DI container
    // resolves and injects these dependencies when the service is created.
    private readonly TransactionDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<TransactionService> _logger;

    // Constructor injection: ASP.NET Core's DI container automatically provides
    // instances of these types when creating a TransactionService.
    public TransactionService(
        TransactionDbContext dbContext,
        IEventBus eventBus,
        ILogger<TransactionService> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _logger = logger;
    }

    // "async Task<Transaction>" means this method runs asynchronously and
    // eventually returns a Transaction object. The caller uses "await" to
    // get the result without blocking a thread.
    public async Task<Transaction> CreateTransactionAsync(CreateTransactionRequest request)
    {
        if (request.Amount <= 0)
        {
            // nameof(request.Amount) returns "Amount" as a string. If someone
            // renames the parameter, the compiler will catch the error.
            throw new ArgumentException("Transaction amount must be greater than zero.", nameof(request.Amount));
        }

        // Object initializer syntax - the curly braces after "new Transaction"
        // set property values inline instead of calling setters separately.
        var transaction = new Transaction
        {
            // Guid.NewGuid() generates a globally unique identifier (128-bit).
            // .ToString() produces a hyphenated hex string like "a1b2c3d4-..."
            Id = Guid.NewGuid().ToString(),
            UserId = request.UserId,
            Amount = request.Amount,
            Currency = request.Currency,
            // DateTime.UtcNow gets the current UTC time (timezone-independent).
            Timestamp = DateTime.UtcNow,
            Status = TransactionStatus.Pending,
            Description = request.Description,
            Counterparty = request.Counterparty,
            Pan = request.Pan,
            PinBlock = request.PinBlock,
            CardType = request.CardType
        };

        _dbContext.Transactions.Add(transaction);

        // SaveChangesAsync() sends all pending changes (inserts, updates, deletes)
        // to the database in a single transaction. "await" waits for the database
        // to confirm the write before continuing.
        await _dbContext.SaveChangesAsync();

        ServiceMetrics.TransactionsCreatedTotal.Inc();

        // _logger.LogInformation() logs at the Information level. The message
        // template uses {PropertyName} placeholders (not $ string interpolation).
        // These become structured properties in the log output, enabling powerful
        // filtering and analysis in log management systems.
        _logger.LogInformation("Created transaction {TransactionId} for user {UserId} with amount {Amount} {Currency}",
            transaction.Id, transaction.UserId, transaction.Amount, transaction.Currency);

        // Create a domain event to notify other services about the new transaction.
        var createdEvent = new TransactionCreatedEvent(
            transaction.Id,
            transaction.UserId,
            transaction.Amount,
            transaction.Currency,
            transaction.Timestamp,
            transaction.Pan,
            transaction.PinBlock,
            transaction.CardType
        );

        // Publish the event to the event bus so other microservices can react.
        await _eventBus.PublishAsync(createdEvent);

        ServiceMetrics.EventsPublishedTotal.WithLabels("TransactionCreated").Inc();

        _logger.LogInformation("Published TransactionCreatedEvent for transaction {TransactionId}", transaction.Id);

        return transaction;
    }

    // "Transaction?" - the question mark means the return type is nullable.
    // This method may return null if no transaction with the given ID exists.
    public async Task<Transaction?> GetTransactionAsync(string id)
    {
        // FindAsync() looks up an entity by its primary key. It first checks the
        // local change tracker (in-memory cache) and only queries the database if
        // not found locally. This makes repeated lookups for the same entity fast.
        return await _dbContext.Transactions.FindAsync(id);
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByUserAsync(string userId)
    {
        // This chain of LINQ methods builds a SQL query:
        //   1. Where(t => t.UserId == userId) - adds a SQL WHERE clause
        //   2. OrderByDescending(t => t.Timestamp) - adds ORDER BY Timestamp DESC
        //   3. ToListAsync() - executes the query and returns results as a List
        //
        // The "t =>" syntax is a lambda expression - an inline function where
        // "t" is the parameter (each Transaction in the sequence).
        return await _dbContext.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetAllTransactionsAsync()
    {
        return await _dbContext.Transactions
            .OrderByDescending(t => t.Timestamp)
            .ToListAsync();
    }
}
