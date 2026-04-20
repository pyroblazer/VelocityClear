// ============================================================================
// HsmModels.cs - Data Transfer Objects (DTOs) for HSM Operations
// ============================================================================
// This file defines the request/response types for all HSM operations.
// In C#, "record" creates lightweight immutable data types with built-in
// equality comparison, similar to data classes in Kotlin or Python's
// named tuples with type safety.
//
// Banking domain concepts:
//   ZMK = Zone Master Key (encrypts other keys for distribution)
//   ZPK = Zone PIN Key (encrypts PINs during transport)
//   PVK = PIN Validation Key (generates/verifies PIN offsets)
//   CVK = Card Verification Key (generates CVV/CVC values)
//   TAK = Terminal Authentication Key (secures terminal comms)
//   KCV = Key Check Value (verifies key import correctness)
//   LMK = Local Master Key (root key inside the HSM)
//
// Key C# concepts:
//   "enum" - A named set of integer constants. HsmKeyType.ZPK == 1.
//   "record" - Immutable reference type with value-based equality.
//     Two records are equal if all their properties are equal.
// ============================================================================

namespace FinancialPlatform.PinEncryptionService.Models;

// Enumerates the types of cryptographic keys managed by the HSM.
// Each key type serves a different purpose in the payment processing flow.
public enum HsmKeyType { ZMK, ZPK, PVK, CVK, TAK }

// Request to generate a new cryptographic key.
public record GenerateKeyRequest(HsmKeyType KeyType, string KeyId);

// Response after generating a key. Includes the Key Check Value (KCV)
// which is a 6-hex-char hash of the key used to verify correct import.
// EncryptedUnderLmk is the key encrypted under the HSM's Local Master Key.
public record GenerateKeyResponse(
    string KeyId,
    HsmKeyType KeyType,
    string KeyCheckValue,
    string EncryptedUnderLmk);

// Request to encrypt a cleartext PIN under a Zone PIN Key (ZPK).
public record EncryptPinRequest(string Pin, string Pan, string ZpkId);

// Response containing the encrypted PIN block, the ZPK's KCV, and the format used.
public record EncryptPinResponse(
    string EncryptedPinBlock,
    string KeyCheckValue,
    string Format);

// Request to decrypt a PIN block back to cleartext.
public record DecryptPinRequest(string EncryptedPinBlock, string Pan, string ZpkId);

// Response with the decrypted cleartext PIN digits and the format used.
public record DecryptPinResponse(string Pin, string Format);

// Request to translate a PIN block from one ZPK to another (zone-to-zone transfer).
// The PIN is decrypted under the source ZPK and re-encrypted under the destination ZPK.
public record TranslatePinRequest(
    string EncryptedPinBlock,
    string SourceZpkId,
    string DestZpkId,
    string Pan);

// Response with the PIN block re-encrypted under the destination ZPK.
public record TranslatePinResponse(string EncryptedPinBlock, string Format);

// Request to verify an encrypted PIN block against an expected cleartext PIN.
// Used to check if a cardholder entered the correct PIN.
public record VerifyPinRequest(
    string EncryptedPinBlock,
    string Pan,
    string ZpkId,
    string ExpectedPin);

// Response indicating whether the PIN matched.
public record VerifyPinResponse(bool Verified, string Message);

// Internal representation of a key stored in the HSM key store.
// The key is stored encrypted under the LMK (EncryptedValue).
// Not exposed via API - used internally by SoftwareHsmService.
public record StoredKey(
    string KeyId,
    HsmKeyType KeyType,
    byte[] EncryptedValue,
    string CheckValue,
    KeyStatus Status = KeyStatus.Active,
    DateTime? RotatedAt = null);

// Key lifecycle status for rotation tracking.
public enum KeyStatus { Active, Inactive, Rotated }

// Request to rotate an existing key (generate a replacement).
public record RotateKeyRequest(string ExistingKeyId, string NewKeyId);

// Response after rotating a key.
public record RotateKeyResponse(
    string OldKeyId,
    string NewKeyId,
    string OldKcv,
    string NewKcv);
