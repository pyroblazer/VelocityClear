// ============================================================================
// User.cs - User Entity Model
//
// This file defines the User entity. Each user of the financial platform (admin,
// auditor, regular user, guest) is represented by an instance of this class.
// The password is stored as a hash (never plaintext) for security.
// This entity maps to a database table via Entity Framework Core.
// ============================================================================

using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

public class User
{
    // Unique identifier for the user (typically a GUID converted to string).
    public string Id { get; set; } = string.Empty;

    // The user's login name.
    public string Username { get; set; } = string.Empty;

    // The bcrypt/scrypt hash of the user's password. Storing hashes instead of
    // plaintext passwords is a fundamental security practice.
    public string PasswordHash { get; set; } = string.Empty;

    // "UserRole" is an enum defined elsewhere in this project. It restricts
    // the role to one of: Guest, User, Admin, or Auditor. Using an enum instead
    // of a free-form string prevents typos and invalid values.
    public UserRole Role { get; set; }

    // Records when the user account was created. Default is the current UTC time.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
