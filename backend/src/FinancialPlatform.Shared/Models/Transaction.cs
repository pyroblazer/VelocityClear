// ============================================================================
// Transaction.cs - Transaction Entity Model
//
// This file defines the Transaction entity, which is the core domain object in
// the platform. Every financial transaction (e.g., a money transfer, a payment)
// is represented as an instance of this class. It maps directly to a database
// table via Entity Framework Core (the ORM used in this project).
//
// A "class" in C# is a reference type that defines a blueprint for objects,
// similar to classes in Java or constructor functions/classes in TypeScript.
// Instances are created with the "new" keyword: var tx = new Transaction();
// ============================================================================

using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

// "public" means this class is visible to all other code in the project.
// Without "public", the class would only be accessible within the same file.
public class Transaction
{
    // "{ get; set; }" declares a C# property with both a getter and a setter.
    // Properties are C#'s idiomatic way to expose data - they look like fields
    // but are actually methods under the hood. This is similar to using
    // get/set accessors in JavaScript classes or Java beans.
    //
    // "= string.Empty" is an initializer that sets the default value.
    // "string.Empty" is an empty string "", preferred over "" for clarity.
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    // "decimal" is a 128-bit numeric type in C# designed for financial and monetary
    // calculations. Unlike "float" or "double", decimal avoids rounding errors
    // because it represents numbers in base-10 internally. Always use decimal for
    // money - never float or double.
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    // "DateTime" is C#'s struct (value type) representing a date and time.
    // "DateTime.UtcNow" returns the current UTC (Coordinated Universal Time).
    // Using UTC avoids timezone-related bugs in distributed systems.
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // This property uses the TransactionStatus enum as its type, restricting
    // the value to one of the defined enum members (Pending, Approved, etc.).
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    // The "?" suffix on "string?" means this is a nullable reference type.
    // By default in C# (with nullable reference types enabled), reference types
    // like string cannot be null. Adding "?" explicitly allows null values.
    // This is similar to TypeScript's "string | null" union type.
    public string? Description { get; set; }

    public string? Counterparty { get; set; }

    public string? Pan { get; set; }

    public string? PinBlock { get; set; }

    public string? CardType { get; set; }
}
