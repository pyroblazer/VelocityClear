// ============================================================================
// JwtService.cs - JSON Web Token Generation Service
// ============================================================================
// This service generates JWT (JSON Web Token) tokens for authenticated users.
// A JWT is a compact, URL-safe, self-contained token that securely transmits
// information between parties as a JSON object. It consists of three parts:
//
//   1. HEADER    - algorithm and token type (Base64URL-encoded JSON)
//   2. PAYLOAD   - claims (user data like ID, role, expiration) (Base64URL-encoded JSON)
//   3. SIGNATURE - cryptographic signature verifying the token hasn't been tampered with
//
// The token is signed using HMAC-SHA256 with a shared secret key. Anyone who
// possesses this key can generate and validate tokens, so the key must be
// kept secret. In a microservices architecture, all services that need to
// validate tokens must share the same secret key (or use asymmetric keys).
//
// This service is registered as a singleton in Program.cs so that the same
// instance (with the same configuration) is used throughout the application.
// ============================================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FinancialPlatform.ApiGateway.Services;

public class JwtService
{
    // --------------------------------------------------------------------------
    // Fields prefixed with underscore (_) is a common C# convention for private
    // fields. The "readonly" modifier ensures these values can only be set
    // during object construction, preventing accidental modification.
    // --------------------------------------------------------------------------
    private readonly string _secretKey;
    private readonly string _issuer;

    // --------------------------------------------------------------------------
    // IConfiguration - ASP.NET Core's configuration interface. It provides
    // access to values from appsettings.json, environment variables, command-
    // line arguments, and other configuration sources. The indexer syntax
    // config["Section:Key"] navigates the hierarchical configuration.
    //
    // The "??" (null-coalescing operator) provides a fallback value if the
    // configuration key is not found (returns null). This ensures the service
    // always has valid values even if configuration is missing.
    // --------------------------------------------------------------------------
    public JwtService(IConfiguration config)
    {
        _secretKey = config["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        if (_secretKey.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 characters.");
        _issuer = config["Jwt:Issuer"] ?? "FinancialPlatform";
    }

    /// <summary>
    /// Generates a signed JWT token for the given user.
    ///
    /// Parameters:
    ///   userId - unique identifier for the user (becomes the "sub" claim)
    ///   role   - the user's role (becomes the role claim for authorization)
    ///
    /// Returns: a signed JWT string (three Base64URL segments separated by dots)
    /// </summary>
    public string GenerateToken(string userId, string role)
    {
        // --------------------------------------------------------------------------
        // SymmetricSecurityKey wraps the raw key bytes for use by the JWT library.
        // "Symmetric" means the same key is used for both signing and verification.
        // (The alternative is asymmetric keys like RSA, where signing and verification
        // use different keys.)
        //
        // Encoding.UTF8.GetBytes() converts the string to a byte array using UTF-8
        // encoding. HMAC-SHA256 requires the key as bytes.
        // --------------------------------------------------------------------------
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));

        // SigningCredentials specifies how the token will be signed:
        //   - Which key to use
        //   - Which algorithm (HMAC-SHA256 produces a 256-bit hash)
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // --------------------------------------------------------------------------
        // Claims are key-value pairs stored inside the JWT payload. They represent
        // statements about the user (like "this user's ID is X" or "this user is an admin").
        //
        // "var claims = new[]" creates an implicitly-typed array. The compiler
        // infers the element type as "Claim" from the contents.
        //
        // Common JWT claims:
        //   Sub (Subject) - identifies the principal (the user)
        //   Jti (JWT ID)  - unique identifier for this specific token (prevents replay attacks)
        //
        // ClaimTypes.Role - a URI-style claim type for the user's role, recognized
        //   by ASP.NET Core's authorization system for role-based access control.
        //
        // Guid.NewGuid().ToString() generates a unique token ID to ensure each
        // token is distinct, even if generated for the same user in the same second.
        // --------------------------------------------------------------------------
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),          // Subject: the user ID
            new Claim(ClaimTypes.Role, role),                         // Role: for [Authorize(Roles="...")]
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())  // Unique token ID
        };

        // --------------------------------------------------------------------------
        // JwtSecurityToken - the actual token object containing all the data.
        //
        // Named parameters (issuer:, audience:, etc.) are C#'s "named arguments"
        // feature. They make the code more readable by showing which value maps
        // to which parameter, regardless of order.
        //
        // "expires" sets the "exp" claim - the token is invalid after this time.
        // AddHours(1) creates a DateTime 1 hour in the future from UTC now.
        // --------------------------------------------------------------------------
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        // JwtSecurityTokenHandler.WriteToken() serializes the JwtSecurityToken
        // into the standard JWT string format: "header.payload.signature"
        // (three Base64URL-encoded segments separated by periods).
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
