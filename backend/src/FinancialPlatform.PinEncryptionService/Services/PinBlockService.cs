// ============================================================================
// PinBlockService.cs - ISO 9564-1 Format 0 PIN Block Operations
// ============================================================================
// This service implements PIN block operations following ISO 9564-1 Format 0
// (also known as ANSI X9.8). This is the standard method used by payment
// networks worldwide to encrypt PINs during transport between devices.
//
// How PIN blocks work:
//   1. The cleartext PIN is formatted into a "PIN block" (8 bytes):
//      [0][length][PIN digits][F-padding to 16 hex chars]
//      Example: PIN "1234" -> "041234FFFFFFFFFF"
//
//   2. A "PAN block" is derived from the card number (8 bytes):
//      [0000][12 rightmost digits excluding check digit]
//      This ties the PIN to a specific card.
//
//   3. The two blocks are XOR'd together, then encrypted with 3DES-ECB
//      under a Zone PIN Key (ZPK). The result is the "encrypted PIN block"
//      that gets transmitted in ISO 8583 field 52.
//
//   4. To decrypt, reverse the process: 3DES decrypt -> XOR with PAN block
//      -> extract PIN digits from the cleartext PIN block.
//
// Cryptographic details:
//   - 3DES (Triple DES): Applies DES encryption three times per block.
//     Uses a double-length (16-byte) or triple-length (24-byte) key.
//     A 16-byte key is expanded to 24 bytes via EDE (Encrypt-Decrypt-Encrypt).
//   - ECB mode: Electronic Code Book - each 8-byte block is encrypted
//     independently. Suitable here because PIN blocks are exactly one block.
//   - No padding: PIN blocks are always exactly 8 bytes.
//
// Key C# concepts:
//   Convert.FromHexString() / ToHexString() - Convert between byte arrays
//     and hexadecimal string representations (e.g., "AABB" <-> [0xAA, 0xBB]).
//   TripleDES.Create() - Factory method from System.Security.Cryptography
//     that creates a TripleDES encryptor/decryptor instance.
//   ReadOnlySpan[..3] - Range operator: extracts the first 3 bytes.
// ============================================================================

using System.Security.Cryptography;

namespace FinancialPlatform.PinEncryptionService.Services;

public class PinBlockService
{
    // Builds an unencrypted Format-0 PIN block (8 bytes).
    // Layout: 0 | PIN_LEN (hex) | PIN_DIGITS | FFFF... padded to 16 hex chars.
    // Example: PIN "1234" -> hex "041234FFFFFFFFFF" -> 8 bytes.
    public byte[] BuildPinBlock(string pin)
    {
        if (pin.Length < 4 || pin.Length > 12 || !pin.All(char.IsDigit))
            throw new ArgumentException("PIN must be 4-12 decimal digits.", nameof(pin));

        // $"0{pin.Length:X}" formats the length as a single hex digit (e.g., 4 -> "04").
        // PadRight(16, 'F') fills remaining positions with 'F' (ISO 9564 fill character).
        var hex = $"0{pin.Length:X}{pin}".PadRight(16, 'F');
        return Convert.FromHexString(hex);
    }

    // Builds the PAN block (8 bytes): 0000 + 12 rightmost PAN digits excluding check digit.
    // The check digit is the rightmost digit of the PAN (verified via Luhn algorithm).
    // Example: PAN "4111111111111111" -> exclude last '1' -> take 12 rightmost -> "111111111111"
    //          -> hex "0000111111111111" -> 8 bytes.
    public byte[] BuildPanBlock(string pan)
    {
        pan = new string(pan.Where(char.IsDigit).ToArray());
        if (pan.Length < 13)
            throw new ArgumentException("PAN must have at least 13 digits.", nameof(pan));

        // pan[..^1] excludes the last character (check digit) using the hat operator.
        // [^12..] takes the last 12 characters (rightmost 12 digits).
        var withoutCheck = pan[..^1];
        var pan12 = withoutCheck.Length >= 12
            ? withoutCheck[^12..]
            : withoutCheck.PadLeft(12, '0');

        return Convert.FromHexString($"0000{pan12}");
    }

    // Encrypts a PIN under a ZPK using ISO 9564 Format 0.
    // Steps: Build PIN block -> Build PAN block -> XOR -> 3DES-ECB encrypt.
    // Returns the encrypted PIN block as a 16-character uppercase hex string.
    public string EncryptPin(string pin, string pan, byte[] zpk)
    {
        var pinBlock = BuildPinBlock(pin);
        var panBlock = BuildPanBlock(pan);
        var xored = Xor8(pinBlock, panBlock);
        var encrypted = TripleDesEcb(xored, zpk, encrypt: true);
        return Convert.ToHexString(encrypted);
    }

    // Decrypts a Format-0 PIN block and extracts the cleartext PIN digits.
    // Steps: 3DES-ECB decrypt -> XOR with PAN block -> Extract PIN from block.
    public string DecryptPin(string encryptedHex, string pan, byte[] zpk)
    {
        var encrypted = Convert.FromHexString(encryptedHex);
        var decrypted = TripleDesEcb(encrypted, zpk, encrypt: false);
        var panBlock = BuildPanBlock(pan);
        var pinBlock = Xor8(decrypted, panBlock);
        return ExtractPin(pinBlock);
    }

    // Translates an encrypted PIN from one ZPK to another without exposing cleartext.
    // Used when forwarding a transaction between two banking zones that use different
    // Zone PIN Keys. Steps: Decrypt under source ZPK -> Encrypt under destination ZPK.
    public string TranslatePin(string encryptedHex, byte[] sourceZpk, byte[] destZpk, string pan)
    {
        var decryptedPin = DecryptPin(encryptedHex, pan, sourceZpk);
        return EncryptPin(decryptedPin, pan, destZpk);
    }

    // Calculates the Key Check Value (KCV): first 3 bytes (6 hex chars) of
    // 3DES-ECB encryption of 8 zero bytes under the given key.
    // The KCV is used to verify a key was imported correctly without revealing the key.
    public string CalculateKcv(byte[] key)
    {
        var zeros = new byte[8];
        var encrypted = TripleDesEcb(zeros, key, encrypt: true);
        return Convert.ToHexString(encrypted[..3]);
    }

    // XOR two 8-byte arrays element-by-element. Used to combine PIN and PAN blocks.
    // In C#, (byte)(a[i] ^ b[i]) casts the int result of XOR back to byte.
    private static byte[] Xor8(byte[] a, byte[] b)
    {
        var result = new byte[8];
        for (var i = 0; i < 8; i++)
            result[i] = (byte)(a[i] ^ b[i]);
        return result;
    }

    // Triple DES encryption/decryption in ECB mode with no padding.
    // If a double-length (16-byte) key is provided, it is expanded to triple-length
    // (24-byte) by appending the first 8 bytes: [K1|K2] -> [K1|K2|K1] (EDE pattern).
    private static byte[] TripleDesEcb(byte[] data, byte[] key, bool encrypt)
    {
        // "[.. key, .. key[..8]]" uses collection expressions (C# 12) to concatenate
        // the full 16-byte key with its first 8 bytes, producing a 24-byte EDE key.
        var fullKey = key.Length == 16 ? [.. key, .. key[..8]] : key;

        // "using var" ensures the cryptographic resources are disposed after use.
        using var tdes = TripleDES.Create();
        tdes.Key = fullKey;
        tdes.Mode = CipherMode.ECB;           // Each 8-byte block encrypted independently
        tdes.Padding = PaddingMode.None;       // No padding needed (PIN blocks are exactly 8 bytes)

        using var transform = encrypt ? tdes.CreateEncryptor() : tdes.CreateDecryptor();
        return transform.TransformFinalBlock(data, 0, data.Length);
    }

    // Extracts the PIN digits from a cleartext PIN block.
    // Format: [0][length_hex][digits][F-padding]
    // Example: hex "041234FFFFFFFFFF" -> length=4, digits="1234"
    private static string ExtractPin(byte[] pinBlock)
    {
        var hex = Convert.ToHexString(pinBlock);
        // hex[1] is the length nibble (e.g., '4' -> length 4).
        // int.Parse with HexNumber style converts a hex char to its integer value.
        var len = int.Parse(hex[1].ToString(), System.Globalization.NumberStyles.HexNumber);
        return hex[2..(2 + len)];
    }
}
