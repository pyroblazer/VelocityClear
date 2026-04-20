// ============================================================================
// AuthController.cs - Authentication Endpoints
// ============================================================================
// This controller handles user login and registration. It exposes two HTTP
// endpoints under "api/auth":
//   POST /api/auth/login    - validates credentials and returns a JWT token
//   POST /api/auth/register - creates a new user account
//
// Controllers in ASP.NET Core are classes that handle incoming HTTP requests
// and return HTTP responses. They are similar to route handlers in Express.js
// or controllers in Spring Boot.
// ============================================================================

using FinancialPlatform.ApiGateway.Services;
using FinancialPlatform.ApiGateway;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using BCrypt.Net;

// A "namespace" is a way to organize types, similar to packages in Java or
// modules in Python. It prevents naming collisions between types.
namespace FinancialPlatform.ApiGateway.Controllers;

// --------------------------------------------------------------------------
// [ApiController] - Marks this class as an API controller. This attribute
//   enables several conventions:
//   - Automatic 400 Bad Request for invalid model states (validation errors)
//   - Inference of [FromBody] for complex parameters (no need to write it)
//   - ProblemDetails format for error responses
//
// [Route("api/auth")] - Sets the base URL prefix for all actions in this
//   controller. Combined with [HttpGet]/[HttpPost] on individual methods,
//   this determines the full URL pattern.
// --------------------------------------------------------------------------
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    // --------------------------------------------------------------------------
    // "static readonly" - "static" means this field belongs to the class
    // itself (not to any instance), shared across all requests. "readonly"
    // means the field can only be assigned at declaration or in a constructor.
    //
    // "List<User>" is a dynamically-sized list (like std::vector in C++ or
    // an array in JavaScript). The "new()" syntax with the initializer braces
    // is a "target-typed new expression" - the compiler infers the type.
    //
    // Note: This in-memory list is for demonstration only. In production,
    // users would be stored in a database.
    // --------------------------------------------------------------------------
    // Fixed IDs keep the auth list in sync with the database seed (database/seed.sql).
    // Changing an ID here requires updating seed.sql too.
    private static readonly List<User> _users = new()
    {
        new User
        {
            Id = "a0000000-0000-0000-0000-000000000001",
            Username = "admin",
            PasswordHash = "$2a$12$/lQtv3vrRF.QqWmY1OEsFOtA7GhhDQEo19hrSfk.vfOQdjI0m.HP6",
            Role = UserRole.Admin,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        new User
        {
            Id = "a0000000-0000-0000-0000-000000000002",
            Username = "trader1",
            PasswordHash = "$2a$12$3BAiQTMvl39DdCAYLPyhTOlW7EvlLOi6DQG.vt88GXl80q7kCK9nG",
            Role = UserRole.User,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        new User
        {
            Id = "a0000000-0000-0000-0000-000000000003",
            Username = "auditor1",
            PasswordHash = "$2a$12$DrG9BY1nNKlf3aAOmTBUle2LCymMHolvhutRiWXYzspxyY9EHm8jG",
            Role = UserRole.Auditor,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        new User
        {
            Id = "a0000000-0000-0000-0000-000000000004",
            Username = "testuser",
            PasswordHash = "$2a$12$KMyixRvyieKj/z283wQfLOJg.GfSzNGSR4FxOys.XljRhQYIN2Owy",
            Role = UserRole.User,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }
    };

    // "readonly" field - can only be set in the constructor. This is the
    // idiomatic way to declare injected dependencies in C#.
    private readonly JwtService _jwtService;

    // --------------------------------------------------------------------------
    // Constructor Injection
    // --------------------------------------------------------------------------
    // ASP.NET Core's dependency injection (DI) container automatically
    // provides a JwtService instance when creating this controller. The
    // DI container resolves dependencies declared in constructor parameters.
    // This is called "constructor injection" and is the most common DI pattern.
    // --------------------------------------------------------------------------
    public AuthController(JwtService jwtService)
    {
        _jwtService = jwtService;
    }

    // --------------------------------------------------------------------------
    // [HttpPost("login")] - Maps this method to HTTP POST requests at
    //   /api/auth/login (base route + this route suffix).
    //
    // IActionResult - A flexible return type that can represent any HTTP
    //   response (200 OK, 401 Unauthorized, 404 Not Found, etc.). Methods
    //   like Ok(), Unauthorized(), and BadRequest() return IActionResult
    //   subclasses. This is similar to returning Response objects in Express.
    //
    // [FromBody] - Tells ASP.NET to deserialize the JSON request body into
    //   a LoginRequest object. Because [ApiController] is on the class,
    //   this attribute is actually inferred for complex types.
    // --------------------------------------------------------------------------
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // --------------------------------------------------------------------------
        // LINQ (Language Integrated Query)
        // --------------------------------------------------------------------------
        // FirstOrDefault() is a LINQ method that searches the collection and
        // returns the first matching element, or the default value (null for
        // reference types) if no match is found. This is similar to
        // Array.find() in JavaScript.
        //
        // The "u => ..." syntax is a "lambda expression" - an inline function.
        // "u" is the parameter, and the expression after "=>" is the return
        // value. Similar to "(u) => u.Username === request.Username" in JS.
        // --------------------------------------------------------------------------
        var user = _users.FirstOrDefault(u =>
            u.Username == request.Username && BCrypt.Net.BCrypt.Verify(request.Password, u.PasswordHash));

        // "is null" is C#'s pattern-matching null check. It is equivalent to
        // "user == null" but preferred in modern C# because it cannot be
        // overloaded and is more readable.
        if (user is null)
        {
            // Unauthorized() returns HTTP 401. The anonymous object
            // "new { message = "..." }" is serialized to JSON automatically.
            ServiceMetrics.JwtAuthAttemptsTotal.WithLabels("failure").Inc();
            return Unauthorized(new { message = "Invalid username or password" });
        }

        // Generate a JWT token containing the user's ID and role
        var token = _jwtService.GenerateToken(user.Id, user.Role.ToString());
        var expiresAt = DateTime.UtcNow.AddHours(1);   // Token expires 1 hour from now

        ServiceMetrics.JwtAuthAttemptsTotal.WithLabels("success").Inc();

        // Ok() returns HTTP 200 with the specified object as JSON response body
        return Ok(new LoginResponse(token, user.Role.ToString(), expiresAt));
    }

    // --------------------------------------------------------------------------
    // [HttpPost("register")] - Maps to POST /api/auth/register
    // --------------------------------------------------------------------------
    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        // Any() is a LINQ method that returns true if any element matches the
        // predicate. Similar to Array.some() in JavaScript.
        if (_users.Any(u => u.Username == request.Username))
        {
            // Conflict() returns HTTP 409 - the standard code for duplicate resources
            return Conflict(new { message = "Username already exists" });
        }

        // --------------------------------------------------------------------------
        // Enum.TryParse<UserRole>()
        // --------------------------------------------------------------------------
        // Converts a string to an enum value. "ignoreCase: true" makes it
        // case-insensitive. The "out var role" syntax is an "out parameter" -
        // the method writes the parsed value into "role" if successful.
        // The method returns true if parsing succeeded, false otherwise.
        //
        // "out" parameters are a C# feature where a method can return multiple
        // values - one via the return statement, and others via parameters
        // marked with "out". The caller must declare a variable to receive it.
        // --------------------------------------------------------------------------
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            // BadRequest() returns HTTP 400.
            // The "$" prefix makes this an "interpolated string" - {expression}
            // inside the string is evaluated and inserted. Similar to template
            // literals in JS: `Invalid role: ${value}`.
            //
            // Enum.GetNames<UserRole>() returns all enum value names as strings.
            // string.Join(", ", array) concatenates strings with a separator,
            // like Array.join(", ") in JavaScript.
            return BadRequest(new { message = $"Invalid role. Valid roles: {string.Join(", ", Enum.GetNames<UserRole>())}" });
        }

        var user = new User
        {
            // Guid.NewGuid() generates a Version 4 (random) UUID. .ToString()
            // formats it as lowercase with hyphens: "a1b2c3d4-e5f6-..."
            Id = Guid.NewGuid().ToString(),
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Role = role,
            CreatedAt = DateTime.UtcNow
        };

        _users.Add(user);

        // Anonymous type "new { message = ..., userId = ... }" creates an
        // object with those properties. ASP.NET serializes it to JSON:
        // { "message": "User registered successfully", "userId": "..." }
        return Ok(new { message = "User registered successfully", userId = user.Id });
    }
}
