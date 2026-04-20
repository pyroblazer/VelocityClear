// ============================================================================
// AuthDtos.cs - Authentication Data Transfer Objects (DTOs)
//
// This file defines the data structures used for authentication operations:
// login, registration, and the JWT token response. These are "DTOs" - simple
// data containers used to transfer data between the frontend and API, or between
// service layers. They contain no business logic.
//
// All three types use C#'s "record" keyword for concise, immutable data objects.
// ============================================================================

namespace FinancialPlatform.Shared.DTOs;

// A "record" in C# is a special kind of class designed for immutable data objects.
// It provides built-in value-based equality (two records with the same data are
// considered equal), a nice ToString() implementation, and "with" expressions for
// creating modified copies. This is similar to data classes in Kotlin or
// immutable value objects in other languages.
//
// This "positional record" syntax - record Name(Type param, Type param) -
// automatically creates public init-only properties from the parameters.
// It's a concise way to define simple data carriers.
public record LoginRequest(string Username, string Password);

// The response sent back after successful authentication. Contains the JWT token
// (which the frontend stores and sends with subsequent requests), the user's role,
// and the token's expiration time.
public record LoginResponse(string Token, string Role, DateTime ExpiresAt);

// Used when a new user registers. The Role is provided as a string rather than
// the UserRole enum because DTOs typically use primitive types for serialization
// compatibility across network boundaries.
public record RegisterRequest(string Username, string Password, string Role);
