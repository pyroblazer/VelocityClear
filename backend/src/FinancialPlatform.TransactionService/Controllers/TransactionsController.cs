// ============================================================================
// TransactionsController.cs - HTTP API Controller for Transactions
// ============================================================================
// This controller exposes REST endpoints for creating and querying financial
// transactions. In ASP.NET Core, controllers are classes that handle HTTP requests
// and return HTTP responses. The [ApiController] and [Route] attributes configure
// automatic behaviors like model validation and URL routing.
//
// Key concepts:
//   - [ApiController]: Enables API-specific behaviors (automatic 400 responses
//     for validation errors, inference of [FromBody] for complex types, etc.)
//   - [Route("api/[controller]")]: Sets the URL prefix. [controller] is replaced
//     with the class name minus "Controller" - so this becomes "api/transactions".
//   - ActionResult<T>: A return type that can represent different HTTP status
//     codes (200 OK, 404 Not Found, 201 Created, etc.) with type safety.
//   - [FromBody]: Tells the framework to deserialize the HTTP request body
//     (JSON) into the specified C# type.
//   - CreatedAtAction(): Returns HTTP 201 (Created) with a Location header
//     pointing to the URL where the newly created resource can be retrieved.
// ============================================================================

using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.TransactionService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.TransactionService.Controllers;

// [ApiController] enables REST-specific conventions: automatic model validation,
// problem details for errors, and inference of parameter sources.
[Authorize]
[ApiController]
// [Route] sets the base URL path for all actions in this controller.
// [controller] is a token that gets replaced with "Transactions" (the class name
// minus the "Controller" suffix). All endpoints will be under /api/transactions.
[Route("api/[controller]")]
// ControllerBase is the base class for API controllers (without view support).
// It provides access to Request, Response, User, and helper methods like Ok(),
// NotFound(), BadRequest(), CreatedAtAction(), etc.
public class TransactionsController : ControllerBase
{
    // Private fields stored with readonly semantics. The underscore prefix (_) is
    // a common C# convention for private fields.
    private readonly Services.TransactionService _transactionService;
    // ILogger<T> provides structured logging. The type parameter T is used as
    // the logging category, which helps filter logs by the class that produced them.
    private readonly ILogger<TransactionsController> _logger;

    // The constructor receives dependencies via Dependency Injection (DI).
    // When ASP.NET Core creates this controller, it automatically resolves and
    // injects the services registered in Program.cs.
    public TransactionsController(
        Services.TransactionService transactionService,
        ILogger<TransactionsController> logger)
    {
        _transactionService = transactionService;
        _logger = logger;
    }

    // [HttpPost] maps this method to HTTP POST requests at /api/transactions.
    [HttpPost]
    // Task<ActionResult<T>> is the async return type. "Task" represents an
    // operation that will complete in the future (similar to Promise in JavaScript).
    // ActionResult<T> wraps the return value so you can return different HTTP
    // status codes from the same method.
    public async Task<ActionResult<TransactionResponse>> CreateTransaction(
        // [FromBody] tells the model binder to read the request body (JSON) and
        // deserialize it into a CreateTransactionRequest object.
        [FromBody] CreateTransactionRequest request)
    {
        try
        {
            // "await" pauses this method until the async operation completes,
            // freeing the thread to handle other requests in the meantime.
            var transaction = await _transactionService.CreateTransactionAsync(request);

            // Construct the response DTO (Data Transfer Object) from the entity.
            // A record type (TransactionResponse) is used for immutable data transfer.
            var response = new TransactionResponse(
                transaction.Id,
                transaction.UserId,
                transaction.Amount,
                transaction.Currency,
                transaction.Status.ToString(),
                transaction.Timestamp,
                transaction.Description,
                transaction.Counterparty,
                transaction.Pan,
                transaction.CardType
            );

            // CreatedAtAction() returns HTTP 201 (Created) with:
            //   - A Location header pointing to the "GetTransaction" endpoint URL
            //   - The route parameter { id = transaction.Id }
            //   - The response body
            // nameof(GetTransaction) returns the string "GetTransaction" in a
            // refactoring-safe way - if you rename the method, the compiler will
            // catch the mismatch.
            return CreatedAtAction(
                nameof(GetTransaction),
                new { id = transaction.Id },
                response);
        }
        catch (ArgumentException ex)
        {
            // _logger.LogWarning() logs at the Warning level. The string uses
            // structured logging: {Message} is a placeholder that becomes a
            // property in the log output, not just string interpolation.
            _logger.LogWarning("Invalid transaction creation request: {Message}", ex.Message);

            // BadRequest() returns HTTP 400 with the specified body.
            return BadRequest(new { error = ex.Message });
        }
    }

    // [HttpGet] maps to HTTP GET at /api/transactions (no additional route segment).
    [HttpGet]
    // IEnumerable<T> is a read-only collection interface - more flexible than
    // List<T> because it can represent any sequence (arrays, lists, query results).
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetAllTransactions()
    {
        var transactions = await _transactionService.GetAllTransactionsAsync();

        // .Select() is a LINQ method that projects each element - similar to
        // JavaScript's .map(). Here it converts each Transaction entity into a
        // TransactionResponse DTO.
        var responses = transactions.Select(t => new TransactionResponse(
            t.Id,
            t.UserId,
            t.Amount,
            t.Currency,
            t.Status.ToString(),
            t.Timestamp,
            t.Description,
            t.Counterparty,
            t.Pan,
            t.CardType
        ));

        // Ok() wraps the result in an HTTP 200 (OK) response.
        return Ok(responses);
    }

    // [HttpGet("{id}")] maps to HTTP GET at /api/transactions/{id} where {id}
    // is a route parameter extracted from the URL.
    [HttpGet("{id}")]
    public async Task<ActionResult<TransactionResponse>> GetTransaction(string id)
    {
        var transaction = await _transactionService.GetTransactionAsync(id);

        // "is null" is the C# pattern matching syntax for null checking.
        // It's preferred over "==" because it can't be overloaded by operators.
        if (transaction is null)
        {
            // NotFound() returns HTTP 404 (Not Found).
            // The "new { error = ... }" creates an anonymous type that gets
            // serialized to JSON: { "error": "Transaction with id '...' not found." }
            return NotFound(new { error = $"Transaction with id '{id}' not found." });
        }

        var response = new TransactionResponse(
            transaction.Id,
            transaction.UserId,
            transaction.Amount,
            transaction.Currency,
            transaction.Status.ToString(),
            transaction.Timestamp,
            transaction.Description,
            transaction.Counterparty,
            transaction.Pan,
            transaction.CardType
        );

        return Ok(response);
    }

    // [HttpGet("user/{userId}")] maps to /api/transactions/user/{userId}
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetTransactionsByUser(string userId)
    {
        var transactions = await _transactionService.GetTransactionsByUserAsync(userId);

        var responses = transactions.Select(t => new TransactionResponse(
            t.Id,
            t.UserId,
            t.Amount,
            t.Currency,
            t.Status.ToString(),
            t.Timestamp,
            t.Description,
            t.Counterparty,
            t.Pan,
            t.CardType
        ));

        return Ok(responses);
    }
}
