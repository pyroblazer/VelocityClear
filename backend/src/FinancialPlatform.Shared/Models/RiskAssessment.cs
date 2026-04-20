// ============================================================================
// RiskAssessment.cs - Risk Assessment Entity Model
//
// This file defines the RiskAssessment entity. Every time a transaction is
// created, the RiskService evaluates it and produces a risk assessment. This
// assessment includes a numeric score, a risk level category, and a list of
// flags indicating specific risk factors that were detected (e.g., high velocity,
// unusual amount, suspicious counterparty).
//
// A transaction may be approved, flagged for review, or rejected based on this
// assessment.
// ============================================================================

namespace FinancialPlatform.Shared.Models;

public class RiskAssessment
{
    // Unique identifier for this risk assessment.
    public string Id { get; set; } = string.Empty;

    // Foreign key linking this assessment to the transaction it evaluates.
    public string TransactionId { get; set; } = string.Empty;

    // "int" is a 32-bit integer in C#. The risk score is a numeric value
    // (e.g., 0-100) indicating how risky the transaction is.
    public int Score { get; set; }

    // Risk level category, e.g., "LOW", "MEDIUM", "HIGH", "CRITICAL".
    // Stored as a string for flexibility (could also be an enum).
    public string Level { get; set; } = "LOW";

    // "List<string>" is C#'s generic list collection, equivalent to string[]
    // in TypeScript or ArrayList<String> in Java. "List<T>" is the most commonly
    // used collection type in C#. The "= []" initializer uses C# 12 collection
    // expression syntax to create an empty list.
    public List<string> Flags { get; set; } = [];

    // When this assessment was performed.
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
}
