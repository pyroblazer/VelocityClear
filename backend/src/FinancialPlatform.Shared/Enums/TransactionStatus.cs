// ============================================================================
// TransactionStatus.cs - Transaction Status Enum
//
// This file defines all possible states a transaction can be in throughout its
// lifecycle. Transactions flow through a state machine:
//   Pending -> Processing -> Approved -> Completed
//                      \-> HighRisk -> Rejected
//                      \-> Failed
//
// Using an enum instead of magic strings ensures type safety and prevents typos.
// ============================================================================

namespace FinancialPlatform.Shared.Enums;

// Each member represents a distinct state in the transaction lifecycle.
public enum TransactionStatus
{
    // Initial state when a transaction is first created and awaiting processing.
    Pending,

    // The transaction has passed risk assessment and is approved for payment.
    Approved,

    // The transaction was denied (e.g., failed risk check or payment authorization).
    Rejected,

    // The transaction was flagged as high-risk by the RiskService.
    HighRisk,

    // The transaction is currently being processed through the payment pipeline.
    Processing,

    // The transaction was successfully completed - payment was authorized and settled.
    Completed,

    // An error occurred during processing and the transaction could not be completed.
    Failed
}
