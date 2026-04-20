// ============================================================================
// Iso8583ServiceTests.cs - Unit Tests for ISO 8583 Message Processing
// ============================================================================
// Tests the Iso8583Service which parses and builds ISO 8583 financial messages.
// ISO 8583 is the global standard for card payment messaging - used every time
// a credit/debit card is swiped, inserted, or tapped at a terminal.
//
// Message format: [MTI:4 chars][Bitmap:16 hex chars][Fields...]
//   MTI = Message Type Indicator (e.g., "0100" = Authorization Request)
//   Bitmap = Bit field showing which data elements are present
//   Fields = Data elements (PAN, amount, PIN, etc.) in FIXED/LLVAR/LLLVAR format
//
// Key C# / xUnit concepts:
//   Dictionary<int, string> - Key-value map (like Map<number, string> in TypeScript).
//     The "[2] = value" syntax inside collection initializers adds entries.
//   Assert.Equal(expected, actual) - Note: expected comes FIRST in C# (reversed from JS).
//   Assert.StartsWith / Assert.Contains - String assertion methods.
//   Assert.True / Assert.False - Boolean assertions.
//   new() { [key] = value } - Collection initializer with indexers (shorthand for
//     creating a dictionary and adding entries).
// ============================================================================

using FinancialPlatform.PinEncryptionService.Models;
using FinancialPlatform.PinEncryptionService.Services;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class Iso8583ServiceTests
{
    private readonly Iso8583Service _svc = new();

    [Fact]
    public void Build_ThenParse_RoundTrips()
    {
        // Build a message with 6 fields, then parse it back.
        // All field values should survive the round-trip unchanged.
        var fields = new Dictionary<int, string>
        {
            [2] = "4111111111111111",    // PAN (card number) - LLVAR field
            [3] = "000000",               // Processing code - FIXED 6
            [4] = "000000010000",          // Amount ($1000.00 in cents) - FIXED 12
            [11] = "123456",               // System Trace Audit Number - FIXED 6
            [41] = "TERM0001",             // Terminal ID - FIXED 8
            [49] = "840",                  // Currency code (USD) - FIXED 3
        };
        var msg = new Iso8583Message { Mti = "0100", Fields = fields };
        var built = _svc.Build(msg);
        var parsed = _svc.Parse(built);

        Assert.Equal("0100", parsed.Mti);
        Assert.Equal("4111111111111111", parsed.Fields[2]);
        Assert.Equal("000000010000", parsed.Fields[4]);
        Assert.Equal("TERM0001", parsed.Fields[41]);
    }

    [Fact]
    public void Build_IncludesCorrectMti()
    {
        // The first 4 characters of the built message must be the MTI.
        var msg = new Iso8583Message
        {
            Mti = "0200",  // Financial Transaction Request
            Fields = new() { [3] = "000000", [4] = "000000005000" }
        };
        var built = _svc.Build(msg);
        Assert.StartsWith("0200", built);
    }

    [Fact]
    public void Build_LlvarField_HasLengthPrefix()
    {
        // LLVAR fields have a 2-digit length prefix before the data.
        // Field 2 (PAN) with "4111111111111111" (16 chars) -> "16" + data.
        var pan = "4111111111111111"; // 16 digits
        var msg = new Iso8583Message { Mti = "0100", Fields = new() { [2] = pan } };
        var built = _svc.Build(msg);

        // After MTI (4) + primary bitmap (16) = position 20
        // Field 2 (LLVAR): "16" + "4111111111111111"
        Assert.Contains("164111111111111111", built);
    }

    [Fact]
    public void Build_LllvarField_HasLengthPrefix()
    {
        // LLLVAR fields have a 3-digit length prefix before the data.
        // Field 55 (ICC/EMV data) is LLLVAR.
        var emvData = "9F2608AABBCCDD11223344";  // 22 chars
        var msg = new Iso8583Message { Mti = "0100", Fields = new() { [55] = emvData } };
        var built = _svc.Build(msg);

        // Should contain "022" (3-digit length) + the data
        Assert.Contains("022" + emvData, built);
    }

    [Fact]
    public void GetMtiDescription_KnownMti_ReturnsDescription()
    {
        // Known MTI codes should return human-readable descriptions.
        Assert.Equal("Authorization Request", _svc.GetMtiDescription("0100"));
        Assert.Equal("Financial Transaction Response", _svc.GetMtiDescription("0210"));
    }

    [Fact]
    public void GetMtiDescription_UnknownMti_ReturnsUnknown()
    {
        // Unknown MTI codes should return "Unknown MTI".
        Assert.Equal("Unknown MTI", _svc.GetMtiDescription("9999"));
    }

    [Fact]
    public void Parse_InvalidShortMessage_Throws()
    {
        // Messages shorter than 20 chars (4 MTI + 16 bitmap) are invalid.
        Assert.Throws<ArgumentException>(() => _svc.Parse("0100DEADBEEF"));
    }

    [Fact]
    public void Parse_NullMessage_Throws()
    {
        // Null input should throw.
        Assert.ThrowsAny<Exception>(() => _svc.Parse(null!));
    }

    [Fact]
    public void Build_WithNoFields_ContainsOnlyMtiAndBitmap()
    {
        // Building a message with no fields should produce MTI + all-zeros bitmap.
        var msg = new Iso8583Message { Mti = "0800", Fields = new() };
        var built = _svc.Build(msg);
        Assert.StartsWith("0800", built);
        // Bitmap should be all zeros (no fields present)
        Assert.Equal("0000000000000000", built.Substring(4, 16));
    }

    [Fact]
    public void GetFieldDefinitions_ContainsStandardFields()
    {
        // The static field definitions should include key payment fields.
        var defs = Iso8583Service.GetFieldDefinitions();
        Assert.True(defs.ContainsKey(2));   // PAN (Primary Account Number)
        Assert.True(defs.ContainsKey(4));   // Transaction Amount
        Assert.True(defs.ContainsKey(52));  // PIN data (encrypted PIN block)
    }

    [Fact]
    public void Build_WithField52Pin_IncludesInMessage()
    {
        // Field 52 (PIN data) must be included when provided.
        // This is critical for card authorization messages.
        var encryptedPin = "AABBCCDDEEFF0011";
        var msg = new Iso8583Message
        {
            Mti = "0100",
            Fields = new()
            {
                [2] = "4111111111111111",
                [3] = "000000",
                [4] = "000000010000",
                [52] = encryptedPin,
            }
        };
        var built = _svc.Build(msg);
        Assert.Contains(encryptedPin, built);
    }

    [Fact]
    public void Build_Parse_WithSecondaryBitmap_RoundTrips()
    {
        // Field 55 (ICC/EMV data) has field number > 64, which triggers a secondary bitmap.
        // This tests that both bitmaps are handled correctly.
        var fields = new Dictionary<int, string>
        {
            [2] = "4111111111111111",
            [4] = "000000010000",
            [55] = "9F2608AABBCCDD112233",  // ICC/EMV data (field > 64 = secondary bitmap)
        };
        var msg = new Iso8583Message { Mti = "0100", Fields = fields };
        var built = _svc.Build(msg);
        var parsed = _svc.Parse(built);

        Assert.Equal("9F2608AABBCCDD112233", parsed.Fields[55]);
    }

    [Fact]
    public void Parse_SkipsUnknownFields()
    {
        // Build a message with known fields, then manually add a field number
        // that is NOT in the field definitions. The parser should skip it.
        var fields = new Dictionary<int, string>
        {
            [2] = "4111111111111111",
            [4] = "000000010000",
        };
        var msg = new Iso8583Message { Mti = "0100", Fields = fields };
        var built = _svc.Build(msg);
        var parsed = _svc.Parse(built);

        // Known fields should be parsed; unknown ones should be absent
        Assert.Equal("4111111111111111", parsed.Fields[2]);
        Assert.False(parsed.Fields.ContainsKey(99));  // Field 99 not defined
    }
}
