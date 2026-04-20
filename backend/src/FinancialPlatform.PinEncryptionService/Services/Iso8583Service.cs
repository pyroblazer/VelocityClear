// ============================================================================
// Iso8583Service.cs - ISO 8583 Message Parser and Builder
// ============================================================================
// This service implements parsing and building of ISO 8583 financial messages
// in the ASCII presentation format. ISO 8583 is the international standard
// for financial transaction card-originated interchange messaging, used by
// every major card network (Visa, Mastercard, UnionPay, etc.).
//
// Message structure:
//   [MTI: 4 ASCII chars][Primary Bitmap: 16 hex chars][Fields...]
//
//   MTI (Message Type Indicator) - First 4 characters identify the message type:
//     0100 = Authorization Request (cardholder at a terminal)
//     0110 = Authorization Response (approve or decline from issuer)
//     0200 = Financial Transaction Request (capture/settle the payment)
//     0400 = Reversal Request (undo a previous authorization)
//     0800 = Network Management Request (heartbeat, key exchange)
//
//   Bitmap - 64-bit (or 128-bit) field indicating which data elements are present.
//     Each bit position corresponds to one ISO 8583 field number.
//     If bit 1 is set, a secondary bitmap follows (supporting fields 65-128).
//
//   Fields - Variable-length data elements. Three encoding types:
//     FIXED   = Fixed number of characters (e.g., Field 4 = exactly 12 chars).
//     LLVAR   = 2-digit length prefix + variable data (e.g., Field 2 = "164111...").
//     LLLVAR  = 3-digit length prefix + variable data (e.g., Field 55 = "050...").
//
// Example message (0100 Authorization Request):
//   0100      MTI
//   7200...   Primary bitmap (hex, indicates which fields follow)
//   164111... Field 2 (LLVAR: 16-char PAN)
//   000000    Field 3 (FIXED 6: processing code)
//   000000001000  Field 4 (FIXED 12: amount in cents)
//   ...
//
// Key C# concepts:
//   StringBuilder - Efficient string builder for concatenating many pieces.
//     Avoids creating intermediate string objects (unlike string + string).
//   Dictionary<int, string> - Key-value map where keys are field numbers
//     and values are the field data (string representation).
//   ReadOnlySpan / range operators ([..4], [^12..]) - Efficient slicing
//     of strings/arrays without copying.
// ============================================================================

using System.Text;
using FinancialPlatform.PinEncryptionService.Models;

namespace FinancialPlatform.PinEncryptionService.Services;

public class Iso8583Service
{
    // Static dictionary defining all supported ISO 8583 data elements.
    // Each entry maps a field number to its definition (type, max length, name).
    // "new()" with collection initializer creates and populates the dictionary.
    private static readonly Dictionary<int, FieldDef> Fields = new()
    {
        [2] = new(2, "LLVAR", 19, "Primary Account Number"),
        [3] = new(3, "FIXED", 6, "Processing Code"),
        [4] = new(4, "FIXED", 12, "Transaction Amount"),
        [7] = new(7, "FIXED", 10, "Transmission Date/Time"),
        [11] = new(11, "FIXED", 6, "System Trace Audit Number"),
        [12] = new(12, "FIXED", 6, "Local Transaction Time"),
        [13] = new(13, "FIXED", 4, "Local Transaction Date"),
        [14] = new(14, "FIXED", 4, "Expiration Date"),
        [22] = new(22, "FIXED", 3, "POS Entry Mode"),
        [35] = new(35, "LLVAR", 37, "Track 2 Data"),
        [37] = new(37, "FIXED", 12, "Retrieval Reference Number"),
        [38] = new(38, "FIXED", 6, "Authorization ID Response"),
        [39] = new(39, "FIXED", 2, "Response Code"),
        [41] = new(41, "FIXED", 8, "Card Acceptor Terminal ID"),
        [42] = new(42, "FIXED", 15, "Card Acceptor ID Code"),
        [49] = new(49, "FIXED", 3, "Transaction Currency Code"),
        [52] = new(52, "FIXED", 16, "Personal Identification Number Data"),
        [55] = new(55, "LLLVAR", 999, "ICC / EMV Data"),
    };

    // Human-readable descriptions for common MTI values.
    private static readonly Dictionary<string, string> MtiDescriptions = new()
    {
        ["0100"] = "Authorization Request",
        ["0110"] = "Authorization Response",
        ["0120"] = "Authorization Advice",
        ["0200"] = "Financial Transaction Request",
        ["0210"] = "Financial Transaction Response",
        ["0220"] = "Financial Transaction Advice",
        ["0400"] = "Reversal Request",
        ["0410"] = "Reversal Response",
        ["0420"] = "Reversal Advice",
        ["0800"] = "Network Management Request",
        ["0810"] = "Network Management Response",
    };

    // Parses a raw ISO 8583 message string into a structured Iso8583Message object.
    // Steps: Extract MTI -> Extract bitmap(s) -> Iterate active bits -> Extract field values.
    public Iso8583Message Parse(string isoMessage)
    {
        if (isoMessage.Length < 20)
            throw new ArgumentException("Message too short: must be at least 20 chars (MTI + primary bitmap).");

        var pos = 0;
        var msg = new Iso8583Message();

        // MTI is always the first 4 characters.
        msg.Mti = isoMessage[..4];
        pos = 4;

        // Primary bitmap is always the next 16 hex characters (8 bytes = 64 bits).
        var primaryBitmapHex = isoMessage[4..20];
        pos = 20;

        var primaryBytes = Convert.FromHexString(primaryBitmapHex);
        // If bit 1 of the primary bitmap is set, a secondary bitmap follows
        // (extending support to fields 65-128).
        var hasSecondary = (primaryBytes[0] & 0x80) != 0;

        byte[]? secondaryBytes = null;
        if (hasSecondary)
        {
            secondaryBytes = Convert.FromHexString(isoMessage[20..36]);
            pos = 36;
        }

        // Convert bitmap bytes to a boolean array for easy bit checking.
        var bits = BuildBitset(primaryBytes, secondaryBytes);

        // Iterate through possible field numbers and extract present fields.
        for (var fieldNum = 2; fieldNum <= (hasSecondary ? 128 : 64); fieldNum++)
        {
            // bits is 0-indexed but field numbers start at 2 (bit 1 is the secondary bitmap flag).
            if (!bits[fieldNum - 1]) continue;
            if (!Fields.TryGetValue(fieldNum, out var def)) continue;

            string value;
            switch (def.Type)
            {
                // FIXED fields have a known length - read exactly that many characters.
                case "FIXED":
                    value = isoMessage[pos..(pos + def.MaxLength)];
                    pos += def.MaxLength;
                    break;

                // LLVAR fields have a 2-digit length prefix followed by the data.
                case "LLVAR":
                    {
                        var len = int.Parse(isoMessage[pos..(pos + 2)]);
                        pos += 2;
                        value = isoMessage[pos..(pos + len)];
                        pos += len;
                        break;
                    }

                // LLLVAR fields have a 3-digit length prefix followed by the data.
                default: // LLLVAR
                    {
                        var len = int.Parse(isoMessage[pos..(pos + 3)]);
                        pos += 3;
                        value = isoMessage[pos..(pos + len)];
                        pos += len;
                        break;
                    }
            }

            msg.Fields[fieldNum] = value;
        }

        return msg;
    }

    // Builds a raw ISO 8583 message string from an Iso8583Message object.
    // Steps: Write MTI -> Build bitmap(s) -> Write field values in order.
    public string Build(Iso8583Message msg)
    {
        var sb = new StringBuilder();
        sb.Append(msg.Mti);

        // Sort field numbers to determine bitmap layout.
        var sortedFields = msg.Fields.Keys.OrderBy(k => k).ToList();
        var needsSecondary = sortedFields.Any(k => k > 64);

        var primaryBits = new bool[64];
        var secondaryBits = new bool[64];

        // Bit 1 of primary bitmap indicates a secondary bitmap follows.
        if (needsSecondary)
            primaryBits[0] = true;

        // Set bits for each present field. Fields 1-64 go in primary, 65-128 in secondary.
        foreach (var f in sortedFields)
        {
            if (f > 64) secondaryBits[f - 65] = true;
            else primaryBits[f - 1] = true;
        }

        sb.Append(BitsToHex(primaryBits));
        if (needsSecondary)
            sb.Append(BitsToHex(secondaryBits));

        // Write each field value according to its type definition.
        foreach (var fieldNum in sortedFields)
        {
            if (!Fields.TryGetValue(fieldNum, out var def)) continue;
            var value = msg.Fields[fieldNum];

            switch (def.Type)
            {
                // FIXED: pad or truncate to exact length.
                case "FIXED":
                    sb.Append(value.PadRight(def.MaxLength)[..def.MaxLength]);
                    break;
                // LLVAR: prepend 2-digit length.
                case "LLVAR":
                    sb.Append($"{value.Length:D2}{value}");
                    break;
                // LLLVAR: prepend 3-digit length.
                default: // LLLVAR
                    sb.Append($"{value.Length:D3}{value}");
                    break;
            }
        }

        return sb.ToString();
    }

    // Returns a human-readable description for an MTI code, or "Unknown" if not found.
    public string GetMtiDescription(string mti) =>
        MtiDescriptions.TryGetValue(mti, out var desc) ? desc : "Unknown MTI";

    // Exposes the field definitions for API responses (GET /api/iso8583/fields).
    public static IReadOnlyDictionary<int, FieldDef> GetFieldDefinitions() => Fields;

    // Converts bitmap bytes into a boolean array (128 bits).
    // Each byte contributes 8 bits, with the most significant bit first.
    private static bool[] BuildBitset(byte[] primary, byte[]? secondary)
    {
        var bits = new bool[128];
        SetBits(bits, primary, 0);
        if (secondary != null) SetBits(bits, secondary, 64);
        return bits;
    }

    // Sets bits in the boolean array from bitmap bytes.
    // 0x80 >> bitIdx shifts the high bit right to test each bit position.
    // The & operator masks to check if that specific bit is set.
    private static void SetBits(bool[] bits, byte[] bitmap, int offset)
    {
        for (var byteIdx = 0; byteIdx < bitmap.Length; byteIdx++)
        {
            for (var bitIdx = 0; bitIdx < 8; bitIdx++)
            {
                if ((bitmap[byteIdx] & (0x80 >> bitIdx)) != 0)
                    bits[offset + byteIdx * 8 + bitIdx] = true;
            }
        }
    }

    // Converts a 64-bit boolean array to an 8-byte hex string (16 chars).
    // Each bit position maps to one bit in the output bytes.
    private static string BitsToHex(bool[] bits)
    {
        var bytes = new byte[8];
        for (var i = 0; i < 64; i++)
        {
            if (bits[i])
                bytes[i / 8] |= (byte)(0x80 >> (i % 8));
        }
        return Convert.ToHexString(bytes);
    }
}
