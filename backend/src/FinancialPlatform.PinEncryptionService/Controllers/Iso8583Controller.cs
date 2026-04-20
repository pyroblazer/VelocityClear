// ============================================================================
// Iso8583Controller.cs - ISO 8583 Message Processing REST API
// ============================================================================
// This controller exposes endpoints for building, parsing, and authorizing
// card transactions using the ISO 8583 financial messaging standard.
//
// ISO 8583 is the global standard for financial transaction card-originated
// interchange messaging. Every time you use a credit/debit card, ISO 8583
// messages flow between the terminal, acquiring bank, card network (Visa/
// Mastercard), and issuing bank.
//
// Message structure: [MTI:4 chars][BITMAP:16 hex chars][FIELDS...]
//   MTI (Message Type Indicator) - 4-digit code defining the message type:
//     0100 = Authorization Request (cardholder is at a terminal)
//     0110 = Authorization Response (approve or decline)
//     0200 = Financial Transaction Request (capture the payment)
//     0400 = Reversal Request (undo a previous transaction)
//     0800 = Network Management Request (heartbeat/key exchange)
//   Bitmap - A bit field indicating which data fields are present.
//   Fields - Variable data elements (PAN, amount, PIN, etc.)
//
// Key fields used in this controller:
//   Field 2  = Primary Account Number (PAN, the card number)
//   Field 3  = Processing Code (000000 = purchase)
//   Field 4  = Transaction Amount (12-digit, zero-padded, in minor units)
//   Field 7  = Transmission Date/Time (MMDDHHmmSS)
//   Field 11 = System Trace Audit Number (STAN, unique per transaction)
//   Field 22 = POS Entry Mode (012 = ICC/chip, 011 = manual key)
//   Field 52 = Personal Identification Number Data (encrypted PIN block)
//   Field 49 = Transaction Currency Code (3-digit, e.g., 840 = USD)
// ============================================================================

using FinancialPlatform.PinEncryptionService.Models;
using FinancialPlatform.PinEncryptionService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.PinEncryptionService.Controllers;

[Authorize]
[ApiController]
[Route("api/iso8583")]
public class Iso8583Controller : ControllerBase
{
    private readonly Iso8583Service _iso;
    private readonly IHsmService _hsm;
    private readonly ILogger<Iso8583Controller> _logger;

    public Iso8583Controller(Iso8583Service iso, IHsmService hsm, ILogger<Iso8583Controller> logger)
    {
        _iso = iso;
        _hsm = hsm;
        _logger = logger;
    }

    // GET /api/iso8583/fields - Returns all supported ISO 8583 field definitions.
    [HttpGet("fields")]
    public IActionResult GetFieldDefinitions() =>
        Ok(Iso8583Service.GetFieldDefinitions().Values.OrderBy(f => f.Number));

    // POST /api/iso8583/parse - Parses a raw ISO 8583 message string into MTI + fields.
    // Body: { "isoMessage": "0100F0000000000000000000..." }
    [HttpPost("parse")]
    public IActionResult Parse([FromBody] ParseIso8583Request request)
    {
        if (string.IsNullOrWhiteSpace(request.IsoMessage))
            return BadRequest(new { error = "IsoMessage is required." });

        var msg = _iso.Parse(request.IsoMessage);
        return Ok(new ParseIso8583Response(
            msg.Mti,
            msg.Fields,
            _iso.GetMtiDescription(msg.Mti)));
    }

    // POST /api/iso8583/build - Builds a raw ISO 8583 message from MTI and field values.
    // Body: { "mti": "0100", "fields": { "2": "4111111111111111", "4": "000000001000", "49": "USD" } }
    // Note: Field keys in the JSON are strings ("2", "4") but map to int field numbers internally.
    [HttpPost("build")]
    public IActionResult Build([FromBody] BuildIso8583Request request)
    {
        if (request.Fields is not { Count: > 0 })
            return BadRequest(new { error = "Fields are required." });

        // Convert string keys to int keys (JSON dictionaries have string keys by default).
        var msg = new Iso8583Message { Mti = request.Mti, Fields = request.Fields };
        var built = _iso.Build(msg);
        return Ok(new BuildIso8583Response(built, built.Length));
    }

    // POST /api/iso8583/authorize - Full card authorization flow.
    // Builds a 0100 authorization request with the encrypted PIN in field 52,
    // then evaluates a simple deny-list rule (PANs starting with "4999" are declined).
    //
    // This endpoint demonstrates how ISO 8583, HSM, and PIN encryption work
    // together in a card authorization flow:
    //   1. The card number, amount, and encrypted PIN are provided
    //   2. A complete ISO 8583 0100 message is assembled
    //   3. Authorization logic evaluates the transaction
    //   4. A response with approval/decline is returned
    [HttpPost("authorize")]
    public IActionResult AuthorizeCard([FromBody] AuthorizeCardRequest request)
    {
        if (!_hsm.HasKey(request.ZpkId))
            return NotFound(new { error = $"ZPK '{request.ZpkId}' not found." });

        // STAN (System Trace Audit Number) - a unique 6-digit reference for this transaction.
        var stan = Random.Shared.Next(100000, 999999).ToString();
        var now = DateTime.UtcNow;

        // Assemble the ISO 8583 fields for a 0100 Authorization Request.
        // ((long)(amount * 100)) converts dollars to cents, then formats as 12 digits.
        var fields = new Dictionary<int, string>
        {
            [2] = request.Pan,
            [3] = "000000",                                              // Processing code: purchase
            [4] = ((long)(request.Amount * 100)).ToString("D12"),        // Amount in minor units (cents)
            [7] = now.ToString("MMddHHmmss"),                            // Transmission date/time
            [11] = stan,                                                  // System Trace Audit Number
            [12] = now.ToString("HHmmss"),                                // Local transaction time
            [13] = now.ToString("MMdd"),                                  // Local transaction date
            [22] = "012",                                                 // POS entry mode: ICC/chip
            [41] = request.TerminalId.PadRight(8)[..8],                  // Terminal ID (8 chars)
            [42] = request.MerchantId.PadRight(15)[..15],                // Merchant ID (15 chars)
            [49] = request.Currency[..3],                                 // Currency code (3 chars)
            [52] = request.EncryptedPinBlock,                             // Encrypted PIN block
        };

        // Build the complete ISO 8583 message string.
        var msg = new Iso8583Message { Mti = "0100", Fields = fields };
        var isoMsg = _iso.Build(msg);

        // Simple authorization: approve unless PAN is on the deny list.
        // "4999" prefix is used as a test deny-list pattern.
        var approved = !request.Pan.StartsWith("4999");
        var responseCode = approved ? "00" : "05";                       // 00=approved, 05=declined
        var authId = approved ? Random.Shared.Next(100000, 999999).ToString() : string.Empty;

        _logger.LogInformation(
            "ISO 8583 authorization: PAN ending {PanSuffix}, Amount={Amount} {Currency}, Approved={Approved}",
            request.Pan[^4..], request.Amount, request.Currency, approved);

        return Ok(new AuthorizeCardResponse(
            approved,
            responseCode,
            authId,
            isoMsg,
            approved ? "Approved" : "Declined - Do not honour"));
    }
}
