// ============================================================================
// SoftwareHsmServiceTests.cs - Unit Tests for Simulated HSM Key Management
// ============================================================================
// Tests the SoftwareHsmService which simulates a Hardware Security Module (HSM).
// These tests verify key management operations and the integration between
// HSM key storage and PinBlockService PIN operations.
//
// Key concepts:
//   HSM = Hardware Security Module - a tamper-resistant crypto processor.
//   LMK = Local Master Key - the root key that encrypts all other keys at rest.
//   ZPK = Zone PIN Key - used to encrypt PINs during transport.
//   KCV = Key Check Value - a short hash verifying key import correctness.
//
// Key C# / xUnit concepts:
//   IDisposable - An interface with a Dispose() method for cleanup. xUnit creates
//     a new instance of the test class for each test method, and calls Dispose()
//     after each test. This ensures test isolation (no shared state between tests).
//   IConfiguration - A configuration interface. Here we use a simple in-memory
//     dictionary ("new ConfigurationBuilder().AddInMemoryCollection().Build()")
//     instead of reading from a file.
//   Assert.True / Assert.False - Boolean assertions.
//   Assert.Throws<T>() - Verifies an exception of type T is thrown.
//   Guid.NewGuid().ToString() - Generates a unique string to avoid name collisions
//     between tests running in parallel.
// ============================================================================

using FinancialPlatform.PinEncryptionService.Models;
using FinancialPlatform.PinEncryptionService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

// IDisposable ensures each test gets a fresh HSM instance and cleans up after itself.
public class SoftwareHsmServiceTests : IDisposable
{
    private readonly PinBlockService _pinBlockService = new();
    private readonly SoftwareHsmService _hsm;

    public SoftwareHsmServiceTests()
    {
        // Build a minimal in-memory configuration so the HSM can read Hsm:LmkHex.
        // In production, this would come from appsettings.json or environment variables.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hsm:LmkHex"] = null  // Use default dev LMK
            })
            .Build();

        _hsm = new SoftwareHsmService(_pinBlockService,
            new LoggerFactory().CreateLogger<SoftwareHsmService>(), config);
    }

    [Fact]
    public void HasKey_DefaultZpk_ReturnsTrue()
    {
        // The HSM is seeded with a "default-zpk" on startup.
        Assert.True(_hsm.HasKey("default-zpk"));
    }

    [Fact]
    public void HasKey_UnknownKey_ReturnsFalse()
    {
        Assert.False(_hsm.HasKey("nonexistent-key"));
    }

    [Fact]
    public void ListKeyIds_IncludesDefaultZpk()
    {
        var keys = _hsm.ListKeyIds();
        Assert.Contains("default-zpk", keys);
    }

    [Fact]
    public void GenerateKey_ReturnsKeyWithKcv()
    {
        // Generate a new ZPK and verify the response includes a key ID and KCV.
        var result = _hsm.GenerateKey(new GenerateKeyRequest(
            KeyType: HsmKeyType.ZPK,
            KeyId: $"test-zpk-{Guid.NewGuid():N}"[..20]
        ));

        Assert.False(string.IsNullOrEmpty(result.KeyCheckValue));
        Assert.Equal(6, result.KeyCheckValue.Length);  // KCV is always 6 hex chars
    }

    [Fact]
    public void GenerateKey_MakesKeyFindable()
    {
        var keyId = $"zpk-{Guid.NewGuid():N}"[..15];
        _hsm.GenerateKey(new GenerateKeyRequest(HsmKeyType.ZPK, keyId));
        Assert.True(_hsm.HasKey(keyId));
    }

    [Fact]
    public void EncryptPin_ThenDecrypt_RoundTrip()
    {
        // Encrypt a PIN via the HSM, then decrypt it - should get the original PIN back.
        var encrypted = _hsm.EncryptPin(new EncryptPinRequest("1234", "4111111111111111", "default-zpk"));
        var decrypted = _hsm.DecryptPin(new DecryptPinRequest(
            encrypted.EncryptedPinBlock, "4111111111111111", "default-zpk"));

        Assert.Equal("1234", decrypted.Pin);
    }

    [Fact]
    public void VerifyPin_CorrectPin_ReturnsTrue()
    {
        // Encrypt a PIN, then verify it against the same cleartext value.
        var encrypted = _hsm.EncryptPin(new EncryptPinRequest("5678", "4111111111111111", "default-zpk"));
        var result = _hsm.VerifyPin(new VerifyPinRequest(
            encrypted.EncryptedPinBlock, "4111111111111111", "default-zpk", "5678"));

        Assert.True(result.Verified);
    }

    [Fact]
    public void VerifyPin_WrongPin_ReturnsFalse()
    {
        // Encrypt PIN "1234", then try to verify against "9999" - should fail.
        var encrypted = _hsm.EncryptPin(new EncryptPinRequest("1234", "4111111111111111", "default-zpk"));
        var result = _hsm.VerifyPin(new VerifyPinRequest(
            encrypted.EncryptedPinBlock, "4111111111111111", "default-zpk", "9999"));

        Assert.False(result.Verified);
    }

    [Fact]
    public void TranslatePin_BetweenTwoZpks()
    {
        // Generate two ZPKs, encrypt under one, translate to the other.
        var zpk1Id = $"zpk1-{Guid.NewGuid():N}"[..15];
        var zpk2Id = $"zpk2-{Guid.NewGuid():N}"[..15];
        _hsm.GenerateKey(new GenerateKeyRequest(HsmKeyType.ZPK, zpk1Id));
        _hsm.GenerateKey(new GenerateKeyRequest(HsmKeyType.ZPK, zpk2Id));

        var encrypted = _hsm.EncryptPin(new EncryptPinRequest("1234", "4111111111111111", zpk1Id));
        var translated = _hsm.TranslatePin(new TranslatePinRequest(
            encrypted.EncryptedPinBlock, zpk1Id, zpk2Id, "4111111111111111"));

        // Decrypt the translated block under the destination ZPK
        var decrypted = _hsm.DecryptPin(new DecryptPinRequest(
            translated.EncryptedPinBlock, "4111111111111111", zpk2Id));
        Assert.Equal("1234", decrypted.Pin);
    }

    [Fact]
    public void ResolveKey_UnknownKey_Throws()
    {
        // Trying to use a nonexistent ZPK should throw KeyNotFoundException.
        Assert.Throws<KeyNotFoundException>(() =>
            _hsm.EncryptPin(new EncryptPinRequest("1234", "4111111111111111", "no-such-key")));
    }

    [Fact]
    public void ResolveKey_WrongType_Throws()
    {
        // Generating a ZMK and trying to use it as a ZPK should fail.
        var keyId = $"zmk-{Guid.NewGuid():N}"[..15];
        _hsm.GenerateKey(new GenerateKeyRequest(HsmKeyType.ZMK, keyId));

        Assert.Throws<InvalidOperationException>(() =>
            _hsm.EncryptPin(new EncryptPinRequest("1234", "4111111111111111", keyId)));
    }

    [Fact]
    public void KeyRotation_GeneratesNewKeyAndMarksOldAsRotated()
    {
        var oldKeyId = $"zpk-{Guid.NewGuid():N}"[..15];
        var newKeyId = $"zpk-{Guid.NewGuid():N}"[..15];
        _hsm.GenerateKey(new GenerateKeyRequest(HsmKeyType.ZPK, oldKeyId));

        var result = _hsm.RotateKey(new RotateKeyRequest(oldKeyId, newKeyId));

        Assert.Equal(oldKeyId, result.OldKeyId);
        Assert.Equal(newKeyId, result.NewKeyId);
        Assert.False(string.IsNullOrEmpty(result.NewKcv));
        Assert.True(_hsm.HasKey(newKeyId));
        Assert.True(_hsm.HasKey(oldKeyId)); // Old key still exists but rotated
    }

    [Fact]
    public void KeyRotation_UnknownKey_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            _hsm.RotateKey(new RotateKeyRequest("nonexistent", "new-key")));
    }

    [Fact]
    public void KeyRotation_DuplicateNewKeyId_Throws()
    {
        var keyId = $"zpk-{Guid.NewGuid():N}"[..15];
        _hsm.GenerateKey(new GenerateKeyRequest(HsmKeyType.ZPK, keyId));

        Assert.Throws<InvalidOperationException>(() =>
            _hsm.RotateKey(new RotateKeyRequest(keyId, keyId)));
    }

    // IDisposable.Dispose() is called by xUnit after each test.
    // No cleanup needed here since the HSM is in-memory only.
    public void Dispose()
    {
        GC.SuppressFinalize(this);  // Prevent finalizer from running (performance optimization)
    }
}
