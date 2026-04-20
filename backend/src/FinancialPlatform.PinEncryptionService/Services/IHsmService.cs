// ============================================================================
// IHsmService.cs - Hardware Security Module Service Interface
// ============================================================================
// Defines the contract for HSM operations. The interface abstracts away the
// implementation details so the rest of the codebase doesn't need to know
// whether it's talking to a real HSM or a software simulation.
//
// In C#, an "interface" is like a contract or protocol - it defines method
// signatures without implementations. Classes that implement the interface
// must provide concrete implementations for all methods.
//
// Dependency injection uses interfaces: controllers and event handlers depend
// on IHsmService (the interface), and the DI container provides the actual
// implementation (SoftwareHsmService) at runtime.
// ============================================================================

using FinancialPlatform.PinEncryptionService.Models;

namespace FinancialPlatform.PinEncryptionService.Services;

public interface IHsmService
{
    // Generates a new cryptographic key and stores it encrypted under the LMK.
    GenerateKeyResponse GenerateKey(GenerateKeyRequest request);

    // Encrypts a cleartext PIN under a ZPK using ISO 9564 Format 0.
    EncryptPinResponse EncryptPin(EncryptPinRequest request);

    // Decrypts an encrypted PIN block back to cleartext PIN digits.
    DecryptPinResponse DecryptPin(DecryptPinRequest request);

    // Translates a PIN block from one ZPK to another (zone-to-zone transfer).
    TranslatePinResponse TranslatePin(TranslatePinRequest request);

    // Verifies an encrypted PIN block against an expected cleartext PIN.
    VerifyPinResponse VerifyPin(VerifyPinRequest request);

    // Checks whether a key with the given ID exists in the key store.
    bool HasKey(string keyId);

    // Lists all key IDs currently stored in the HSM.
    IReadOnlyList<string> ListKeyIds();

    // Rotates an existing key by generating a replacement and marking the old key as rotated.
    RotateKeyResponse RotateKey(RotateKeyRequest request);
}
