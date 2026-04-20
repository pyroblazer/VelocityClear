// ============================================================================
// Iso8583Models.cs - Data Transfer Objects (DTOs) for ISO 8583 Operations
// ============================================================================
// Defines the data types used by the ISO 8583 message parser/builder
// and the card authorization endpoint.
//
// Key C# concepts:
//   "class" vs "record" - Classes are mutable reference types; records are
//     immutable with value-based equality. Iso8583Message uses "class" because
//     its Fields dictionary needs to be mutable during construction.
//   "Dictionary<int, string>" - Maps integer field numbers to string values.
//     Equivalent to Map<number, string> in TypeScript or dict in Python.
//   "{ get; set; }" - Auto-implemented properties with a getter and setter.
//     "= string.Empty" provides a default value.
// ============================================================================

namespace FinancialPlatform.PinEncryptionService.Models;

// Represents a parsed or to-be-built ISO 8583 message.
// MTI (Message Type Indicator): 4-character code (e.g., "0100" = Authorization Request).
// Fields: Dictionary mapping field numbers (2, 3, 4, ...) to their string values.
public class Iso8583Message
{
    public string Mti { get; set; } = string.Empty;
    public Dictionary<int, string> Fields { get; set; } = new();
}

// Defines a single ISO 8583 data element (field).
// Type can be "FIXED" (exact length), "LLVAR" (2-digit length prefix), or "LLLVAR" (3-digit).
public record FieldDef(int Number, string Type, int MaxLength, string Name);

// Request to parse a raw ISO 8583 message string into structured data.
public record ParseIso8583Request(string IsoMessage);

// Response containing the parsed MTI, field values, and human-readable MTI description.
public record ParseIso8583Response(
    string Mti,
    Dictionary<int, string> Fields,
    string MtiDescription);

// Request to build a raw ISO 8583 message from an MTI and field values.
public record BuildIso8583Request(string Mti, Dictionary<int, string> Fields);

// Response containing the assembled message string and its total character length.
public record BuildIso8583Response(string IsoMessage, int TotalLength);

// Request to authorize a card transaction using ISO 8583 messaging.
// Includes card number, encrypted PIN, amount, and merchant details.
public record AuthorizeCardRequest(
    string Pan,
    string EncryptedPinBlock,
    string ZpkId,
    decimal Amount,
    string Currency,
    string TerminalId,
    string MerchantId);

// Response from the card authorization endpoint.
// ResponseCode follows ISO 8583 standards: "00" = approved, "05" = do not honour.
public record AuthorizeCardResponse(
    bool Approved,
    string ResponseCode,
    string AuthorizationId,
    string IsoResponseMessage,
    string Message);
