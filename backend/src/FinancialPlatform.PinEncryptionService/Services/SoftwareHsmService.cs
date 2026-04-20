// ============================================================================
// SoftwareHsmService.cs - Software-Simulated Hardware Security Module (HSM)
// ============================================================================
// This service simulates a physical HSM for development and testing. In
// production banking systems, HSMs are tamper-resistant hardware devices
// (e.g., Thales Luna, AWS CloudHSM) that perform cryptographic operations
// inside a secure boundary.
//
// Key management architecture:
//   LMK (Local Master Key) - The root key stored inside the HSM. All other
//     keys are encrypted (wrapped) under the LMK for storage. The LMK itself
//     never leaves the HSM boundary.
//   ZPK (Zone PIN Key) - A symmetric key used to encrypt PIN blocks during
//     transport between systems. Each banking "zone" has its own ZPK.
//   Key wrapping - Keys are stored encrypted under the LMK using AES-256-ECB.
//     When a key is needed for an operation, it is decrypted in memory,
//     used immediately, then the memory is released.
//
// How this simulation works:
//   1. On startup, a 32-byte LMK is loaded from config (or a dev default).
//   2. A default test ZPK is seeded for development convenience.
//   3. New keys are generated with RandomNumberGenerator (cryptographic RNG).
//   4. Each key is encrypted with AES-256-ECB under the LMK before storage.
//   5. For PIN operations, the ZPK is decrypted, then passed to PinBlockService.
//
// Thread safety: All access to the key store is protected by a lock object,
// ensuring concurrent requests don't corrupt the dictionary.
//
// Key C# concepts:
//   IConfiguration - Reads values from appsettings.json, environment variables,
//     etc. config["Hsm:LmkHex"] navigates { "Hsm": { "LmkHex": "..." } }.
//   lock (_storeLock) - Mutual exclusion: only one thread can enter the block
//     at a time, preventing data corruption in the dictionary.
//   "is { Length: 64 }" - Pattern matching: checks the value is non-null AND
//     has a Length property equal to 64 (a valid 32-byte hex string).
// ============================================================================

using System.Security.Cryptography;
using FinancialPlatform.PinEncryptionService;
using FinancialPlatform.PinEncryptionService.Models;

namespace FinancialPlatform.PinEncryptionService.Services;

public class SoftwareHsmService : IHsmService
{
    // 32-byte AES-256 LMK (Local Master Key) used to encrypt all stored keys.
    // IMPORTANT: This default key is encoded from ASCII text and is NOT for production.
    // Supply Hsm:LmkHex in configuration for real deployments.
    private static readonly byte[] DefaultDevLmk =
        Convert.FromHexString("4E6F74466F7250726F647563744C4D4B4B657956616C75654E6F745072" + "6F6400");

    private readonly byte[] _lmk;
    private readonly PinBlockService _pinBlockService;
    private readonly ILogger<SoftwareHsmService> _logger;
    private readonly Dictionary<string, StoredKey> _keyStore = new();
    private readonly object _storeLock = new();

    public SoftwareHsmService(
        PinBlockService pinBlockService,
        ILogger<SoftwareHsmService> logger,
        IConfiguration config)
    {
        _pinBlockService = pinBlockService;
        _logger = logger;

        // Load the LMK from config, or fall back to the development default.
        // A valid LMK must be 64 hex characters (32 bytes for AES-256).
        var lmkHex = config["Hsm:LmkHex"];
        _lmk = lmkHex is { Length: 64 }
            ? Convert.FromHexString(lmkHex)
            : DefaultDevLmk;

        SeedDefaultKeys();
    }

    // Generates a new random key, calculates its KCV, and stores it encrypted under the LMK.
    public GenerateKeyResponse GenerateKey(GenerateKeyRequest request)
    {
        // RandomNumberGenerator.GetBytes(16) produces 16 cryptographically random bytes.
        // This is the raw key value before encryption.
        var rawKey = RandomNumberGenerator.GetBytes(16);
        var kcv = _pinBlockService.CalculateKcv(rawKey);
        var encryptedKey = EncryptUnderLmk(rawKey);

        var stored = new StoredKey(request.KeyId, request.KeyType, encryptedKey, kcv);

        lock (_storeLock)
            _keyStore[request.KeyId] = stored;

        _logger.LogInformation("Generated {KeyType} key with ID {KeyId}, KCV={Kcv}",
            request.KeyType, request.KeyId, kcv);

        return new GenerateKeyResponse(request.KeyId, request.KeyType, kcv,
            Convert.ToHexString(encryptedKey));
    }

    // Encrypts a cleartext PIN under the specified ZPK using ISO 9564 Format 0.
    public EncryptPinResponse EncryptPin(EncryptPinRequest request)
    {
        var zpk = ResolveKey(request.ZpkId, HsmKeyType.ZPK);
        var kcv = _pinBlockService.CalculateKcv(zpk);
        var encryptedBlock = _pinBlockService.EncryptPin(request.Pin, request.Pan, zpk);

        ServiceMetrics.HsmOperationsTotal.WithLabels("encrypt").Inc();

        // pan[^4..] takes the last 4 characters of the PAN for logging
        // (never log the full PAN for PCI-DSS compliance).
        _logger.LogDebug("Encrypted PIN for PAN ending {PanSuffix}", request.Pan[^4..]);
        return new EncryptPinResponse(encryptedBlock, kcv, "ISO9564-0");
    }

    // Decrypts an encrypted PIN block using the specified ZPK.
    public DecryptPinResponse DecryptPin(DecryptPinRequest request)
    {
        var zpk = ResolveKey(request.ZpkId, HsmKeyType.ZPK);
        var pin = _pinBlockService.DecryptPin(request.EncryptedPinBlock, request.Pan, zpk);
        ServiceMetrics.HsmOperationsTotal.WithLabels("decrypt").Inc();
        return new DecryptPinResponse(pin, "ISO9564-0");
    }

    // Translates a PIN block from one ZPK to another (zone-to-zone transfer).
    public TranslatePinResponse TranslatePin(TranslatePinRequest request)
    {
        var srcZpk = ResolveKey(request.SourceZpkId, HsmKeyType.ZPK);
        var dstZpk = ResolveKey(request.DestZpkId, HsmKeyType.ZPK);
        var translated = _pinBlockService.TranslatePin(
            request.EncryptedPinBlock, srcZpk, dstZpk, request.Pan);
        ServiceMetrics.HsmOperationsTotal.WithLabels("translate").Inc();
        return new TranslatePinResponse(translated, "ISO9564-0");
    }

    // Verifies a PIN block against an expected cleartext PIN.
    public VerifyPinResponse VerifyPin(VerifyPinRequest request)
    {
        var zpk = ResolveKey(request.ZpkId, HsmKeyType.ZPK);
        var decrypted = _pinBlockService.DecryptPin(
            request.EncryptedPinBlock, request.Pan, zpk);

        var verified = decrypted == request.ExpectedPin;
        ServiceMetrics.HsmOperationsTotal.WithLabels("verify").Inc();
        _logger.LogInformation("PIN verification for PAN ending {Suffix}: {Result}",
            request.Pan[^4..], verified ? "PASS" : "FAIL");

        return new VerifyPinResponse(verified, verified ? "PIN correct" : "PIN mismatch");
    }

    // Checks whether a key with the given ID exists in the store.
    public bool HasKey(string keyId)
    {
        lock (_storeLock) return _keyStore.ContainsKey(keyId);
    }

    // Returns a read-only list of all key IDs in the store.
    // "[.. _keyStore.Keys]" uses collection expressions to create a new List<string>.
    public IReadOnlyList<string> ListKeyIds()
    {
        lock (_storeLock) return [.. _keyStore.Keys];
    }

    public RotateKeyResponse RotateKey(RotateKeyRequest request)
    {
        lock (_storeLock)
        {
            if (!_keyStore.TryGetValue(request.ExistingKeyId, out var existingKey))
                throw new KeyNotFoundException($"Key '{request.ExistingKeyId}' not found.");

            if (_keyStore.ContainsKey(request.NewKeyId))
                throw new InvalidOperationException($"Key '{request.NewKeyId}' already exists.");

            // Mark the old key as rotated
            _keyStore[request.ExistingKeyId] = existingKey with
            {
                Status = KeyStatus.Rotated,
                RotatedAt = DateTime.UtcNow
            };

            // Generate a new key of the same type
            var newKeyData = RandomNumberGenerator.GetBytes(16);
            var newKcv = _pinBlockService.CalculateKcv(newKeyData);
            var encryptedNewKey = EncryptUnderLmk(newKeyData);

            _keyStore[request.NewKeyId] = new StoredKey(
                request.NewKeyId, existingKey.KeyType, encryptedNewKey, newKcv);

            _logger.LogInformation("Key rotated: {OldKeyId} -> {NewKeyId} (type: {Type})",
                request.ExistingKeyId, request.NewKeyId, existingKey.KeyType);

            return new RotateKeyResponse(
                request.ExistingKeyId, request.NewKeyId,
                existingKey.CheckValue, newKcv);
        }
    }

    // Resolves a key by ID and type, decrypting it from LMK storage.
    // Throws KeyNotFoundException if the key doesn't exist.
    // Throws InvalidOperationException if the key type doesn't match.
    private byte[] ResolveKey(string keyId, HsmKeyType expectedType)
    {
        StoredKey key;
        lock (_storeLock)
        {
            // "out key!" uses the null-forgiving operator to suppress the
            // compiler's nullable warning (TryGetValue guarantees it's set when true).
            if (!_keyStore.TryGetValue(keyId, out key!))
                throw new KeyNotFoundException($"Key '{keyId}' not found in HSM key store.");
        }

        if (key.KeyType != expectedType)
            throw new InvalidOperationException(
                $"Key '{keyId}' is type {key.KeyType}, expected {expectedType}.");

        return DecryptUnderLmk(key.EncryptedValue);
    }

    // Encrypts key data under the LMK using AES-256-CBC with a random IV.
    // IV is prepended to the ciphertext: [IV (16 bytes)][Ciphertext (32 bytes)].
    private byte[] EncryptUnderLmk(byte[] keyData)
    {
        var padded = new byte[32];
        keyData.CopyTo(padded, 0);

        using var aes = Aes.Create();
        aes.Key = _lmk;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();
        using var enc = aes.CreateEncryptor();
        var encrypted = enc.TransformFinalBlock(padded, 0, padded.Length);

        var result = new byte[aes.IV.Length + encrypted.Length];
        aes.IV.CopyTo(result, 0);
        encrypted.CopyTo(result, aes.IV.Length);
        return result;
    }

    // Decrypts a key that was encrypted under the LMK (AES-256-CBC).
    // Extracts the IV from the first 16 bytes, then decrypts the remainder.
    private byte[] DecryptUnderLmk(byte[] encryptedKey)
    {
        var iv = encryptedKey[..16];
        var ciphertext = encryptedKey[16..];

        using var aes = Aes.Create();
        aes.Key = _lmk;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.IV = iv;
        using var dec = aes.CreateDecryptor();
        var decrypted = dec.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return decrypted[..16];
    }

    // Seeds a well-known test ZPK for development and automated testing.
    // The test key 0123456789ABCDEFFEDCBA9876543210 is a standard double-length
    // 3DES key used in payment system documentation examples.
    private void SeedDefaultKeys()
    {
        var testZpk = Convert.FromHexString("0123456789ABCDEFFEDCBA9876543210");
        var kcv = _pinBlockService.CalculateKcv(testZpk);
        var encryptedZpk = EncryptUnderLmk(testZpk);

        lock (_storeLock)
            _keyStore["default-zpk"] = new StoredKey("default-zpk", HsmKeyType.ZPK, encryptedZpk, kcv);

        _logger.LogInformation("HSM initialized with default-zpk (KCV={Kcv}). DO NOT use in production.", kcv);
    }
}
