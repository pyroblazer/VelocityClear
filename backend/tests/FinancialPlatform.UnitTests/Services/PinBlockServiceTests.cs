// ============================================================================
// PinBlockServiceTests.cs - Unit Tests for ISO 9564 PIN Block Operations
// ============================================================================
// Tests the PinBlockService which implements ISO 9564-1 Format 0 PIN block
// encryption/decryption. These are "unit tests" - they test one class in
// isolation without any database, network, or external dependencies.
//
// Key C# / xUnit testing concepts:
//   [Fact]         - Marks a method as a single test case (no parameters).
//   [Theory]       - Marks a method as a parameterized test (runs once per data row).
//   [InlineData]   - Provides inline data for a [Theory] test. Each attribute
//                    is one set of arguments. The test method runs once per row.
//   Assert.Equal   - Checks that two values are equal (like expect().toBe() in JS).
//   Assert.Throws  - Verifies that a specific exception type is thrown.
//   Assert.Matches - Checks that a string matches a regex pattern.
//   _svc           - The "system under test" (SUT). Created once and reused.
//                    "readonly" means it can't be reassigned after construction.
//   Convert.FromHexString() - Converts a hex string like "0123...EF" to a byte[].
// ============================================================================

using FinancialPlatform.PinEncryptionService.Services;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class PinBlockServiceTests
{
    // "new()" creates a fresh instance of PinBlockService. Since this service
    // has no dependencies (no constructor parameters), we can create it directly.
    private readonly PinBlockService _svc = new();

    // Test ZPK key: a well-known 16-byte (double-length 3DES) test key used in
    // payment system documentation examples. In hex: 0123456789ABCDEFFEDCBA9876543210.
    private static readonly byte[] TestZpk =
        Convert.FromHexString("0123456789ABCDEFFEDCBA9876543210");

    // Standard test Visa PAN (Primary Account Number) from payment test data.
    private const string TestPan = "4111111111111111";

    // [Theory] runs this test once for each [InlineData] row. The "pin" parameter
    // comes from the InlineData attribute. This tests PIN lengths from 4 to 12 digits
    // (the valid range per ISO 9564).
    [Theory]
    [InlineData("1234")]           // Minimum PIN length (4 digits)
    [InlineData("123456")]         // Common 6-digit PIN (used in many countries)
    [InlineData("123456789012")]   // Maximum PIN length (12 digits)
    public void EncryptPin_ThenDecrypt_RoundTrips(string pin)
    {
        // Encrypt the PIN, then decrypt the result - should get back the original PIN.
        // This is a "round-trip" test: it verifies encryption and decryption are inverses.
        var encrypted = _svc.EncryptPin(pin, TestPan, TestZpk);
        var decrypted = _svc.DecryptPin(encrypted, TestPan, TestZpk);
        Assert.Equal(pin, decrypted);
    }

    [Fact]
    public void EncryptPin_ProducesHexString16Chars()
    {
        // An ISO 9564 Format 0 PIN block is always exactly 8 bytes (64 bits).
        // When hex-encoded, that's 16 uppercase hex characters (0-9, A-F).
        var encrypted = _svc.EncryptPin("1234", TestPan, TestZpk);
        Assert.Equal(16, encrypted.Length);
        Assert.Matches("^[0-9A-F]{16}$", encrypted);
    }

    [Fact]
    public void BuildPinBlock_Format0Layout()
    {
        // Format 0 PIN block layout: 0 | PIN_LENGTH (hex) | PIN_DIGITS | F-padding
        // For PIN "1234": "04" (0=format0, 4=length) + "1234" + "FFFFFFFFFF" = 16 hex chars
        var block = _svc.BuildPinBlock("1234");
        var hex = Convert.ToHexString(block);
        Assert.Equal(16, hex.Length);
        Assert.StartsWith("04", hex);       // First nibble = 0 (format), second = 4 (length)
        Assert.StartsWith("041234", hex);   // Followed by the actual PIN digits
    }

    [Fact]
    public void BuildPanBlock_Uses12DigitsExcludingCheckDigit()
    {
        // PAN block: 0000 + 12 rightmost digits of PAN (excluding the check digit).
        // PAN "4111111111111111" -> exclude last '1' (check digit) -> take 12 rightmost -> "111111111111"
        var block = _svc.BuildPanBlock(TestPan);
        var hex = Convert.ToHexString(block);
        Assert.StartsWith("0000", hex);     // First 4 hex chars are always "0000"
        Assert.Equal(16, hex.Length);        // 8 bytes = 16 hex chars
    }

    [Fact]
    public void TranslatePin_DifferentKeyProducesDifferentBlock()
    {
        // PIN translation: decrypt under source ZPK, re-encrypt under destination ZPK.
        // The translated block should be different from the original (different key).
        var zpk2 = Convert.FromHexString("FEDCBA98765432100123456789ABCDEF");
        var encrypted = _svc.EncryptPin("1234", TestPan, TestZpk);
        var translated = _svc.TranslatePin(encrypted, TestZpk, zpk2, TestPan);

        // Different key produces different encrypted output
        Assert.NotEqual(encrypted, translated);

        // Decrypting the translated block under the destination key should yield the original PIN
        var decrypted = _svc.DecryptPin(translated, TestPan, zpk2);
        Assert.Equal("1234", decrypted);
    }

    [Fact]
    public void TranslatePin_SameKeyProducesSameBlock()
    {
        // Translating a PIN from a key to the same key should produce the same block.
        var encrypted = _svc.EncryptPin("5678", TestPan, TestZpk);
        var translated = _svc.TranslatePin(encrypted, TestZpk, TestZpk, TestPan);
        Assert.Equal(encrypted, translated);
    }

    [Fact]
    public void CalculateKcv_Returns6HexChars()
    {
        // KCV (Key Check Value): first 3 bytes of 3DES-ECB(8 zero bytes) under the key.
        // Returned as 6 uppercase hex characters.
        var kcv = _svc.CalculateKcv(TestZpk);
        Assert.Equal(6, kcv.Length);
        Assert.Matches("^[0-9A-F]{6}$", kcv);
    }

    [Fact]
    public void CalculateKcv_IsDeterministic()
    {
        // Same key must always produce the same KCV (it's a pure function of the key).
        Assert.Equal(_svc.CalculateKcv(TestZpk), _svc.CalculateKcv(TestZpk));
    }

    [Fact]
    public void CalculateKcv_DifferentKeysProduceDifferentValues()
    {
        // Two different keys should produce different KCVs (extremely unlikely to collide).
        var zpk2 = Convert.FromHexString("FEDCBA98765432100123456789ABCDEF");
        Assert.NotEqual(_svc.CalculateKcv(TestZpk), _svc.CalculateKcv(zpk2));
    }

    // Negative test cases: [Theory] with [InlineData] provides multiple invalid inputs.
    // Assert.Throws<T>() expects the lambda (code block) to throw exception type T.
    [Theory]
    [InlineData("123")]              // Too short (3 digits, minimum is 4)
    [InlineData("1234567890123")]    // Too long (13 digits, maximum is 12)
    [InlineData("abcd")]             // Non-numeric characters
    public void BuildPinBlock_InvalidPin_Throws(string pin)
    {
        // "Assert.Throws<ArgumentException>(() => ...)" is the C# way to test exceptions.
        // It's similar to expect(() => fn()).toThrow() in JavaScript.
        // The "() => _svc.BuildPinBlock(pin)" is a lambda expression (anonymous function).
        Assert.Throws<ArgumentException>(() => _svc.BuildPinBlock(pin));
    }

    [Fact]
    public void BuildPanBlock_TooShortPan_Throws()
    {
        // PAN must have at least 13 digits (shortest standard PAN is 13 digits).
        Assert.Throws<ArgumentException>(() => _svc.BuildPanBlock("123456789012"));
    }

    [Fact]
    public void BuildPanBlock_MinimumValidPan_Succeeds()
    {
        // Exactly 13 digits is the minimum valid PAN length.
        var block = _svc.BuildPanBlock("1234567890123");
        Assert.Equal(8, block.Length);  // PAN block is always 8 bytes
    }

    [Fact]
    public void DecryptPin_CorruptHex_Throws()
    {
        // Providing an invalid hex string should throw a FormatException.
        Assert.Throws<FormatException>(() => _svc.DecryptPin("NOTHEX!!", TestPan, TestZpk));
    }
}
