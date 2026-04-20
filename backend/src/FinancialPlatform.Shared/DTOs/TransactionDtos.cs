// ============================================================================
// TransactionDtos.cs - Transaction Data Transfer Objects (DTOs)
//
// This file defines DTOs for creating and retrieving transactions. DTOs decouple
// the internal domain model (the Transaction class) from the API contract, so
// changes to the database model don't necessarily break the API.
//
// - CreateTransactionRequest: what the frontend sends to create a new transaction
// - TransactionResponse: what the API returns when listing or fetching transactions
// ============================================================================

namespace FinancialPlatform.Shared.DTOs;

// The request payload for creating a new transaction. The "?" suffix on
// Description and Counterparty means those fields are optional (nullable).
// "decimal" is used for the Amount to ensure precise financial calculations
// without floating-point rounding errors.
public record CreateTransactionRequest(
    string UserId,
    decimal Amount,
    string Currency,
    string? Description,
    string? Counterparty,
    string? Pan = null,
    string? PinBlock = null,
    string? CardType = null
);

// The response returned to the client after creating or fetching a transaction.
// It includes the server-assigned "Id", the computed "Status" (as a string rather
// than the enum, for serialization simplicity), and the "Timestamp" set by the server.
// The "Status" is a string here (not the TransactionStatus enum) because JSON
// serialization of enums can vary, and a string is more portable across clients.
public record TransactionResponse(
    string Id,
    string UserId,
    decimal Amount,
    string Currency,
    string Status,
    DateTime Timestamp,
    string? Description,
    string? Counterparty,
    string? Pan = null,
    string? CardType = null
);
