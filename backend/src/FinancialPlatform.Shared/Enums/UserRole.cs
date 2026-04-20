// ============================================================================
// UserRole.cs - User Role Enum
//
// This file defines the roles that control access and permissions in the platform.
// Each user is assigned exactly one role, which determines what actions they can
// perform and what data they can access. This is used in JWT token generation
// and authorization checks throughout the system.
// ============================================================================

namespace FinancialPlatform.Shared.Enums;

public enum UserRole
{
    // Minimal access - can only view public information.
    Guest,

    // Standard user - can create transactions and view their own data.
    User,

    // Full administrative access - can manage users, view all transactions,
    // and configure system settings.
    Admin,

    // Read-only access to audit logs and compliance data - can verify the
    // integrity of the audit trail but cannot modify transactions.
    Auditor
}
