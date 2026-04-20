// ============================================================================
// HsmController.cs - HSM (Hardware Security Module) REST API
// ============================================================================
// This controller exposes endpoints for managing cryptographic keys and
// performing PIN operations through a simulated HSM. In production, these
// operations would be handled by a physical tamper-resistant HSM device.
//
// Banking domain concepts:
//   HSM  - Hardware Security Module: a dedicated crypto processor that
//          safeguards digital keys and performs encryption/decryption.
//   ZPK  - Zone PIN Key: a symmetric key used to encrypt PINs during
//          transport between two systems (e.g., ATM and bank host).
//   ZMK  - Zone Master Key: encrypts ZPKs for secure distribution.
//   PVK  - PIN Validation Key: used to generate/verify PIN offset values.
//   CVK  - Card Verification Key: used to generate CVV/CVC values.
//   TAK  - Terminal Authentication Key: secures terminal communications.
//   KCV  - Key Check Value: a short hash of a key used to verify it was
//          imported correctly without exposing the key itself.
//
// Key C# concepts:
//   [ApiController] - Enables automatic model validation and infers
//     parameter sources (from body, query, route, etc.).
//   [Route("api/hsm")] - All endpoints are prefixed with /api/hsm.
//   IActionResult - A flexible return type for HTTP responses (Ok(),
//     BadRequest(), NotFound(), Conflict(), etc.).
// ============================================================================

using FinancialPlatform.PinEncryptionService.Models;
using FinancialPlatform.PinEncryptionService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialPlatform.PinEncryptionService.Controllers;

[Authorize]
[ApiController]
[Route("api/hsm")]
public class HsmController : ControllerBase
{
    private readonly IHsmService _hsm;
    private readonly ILogger<HsmController> _logger;

    public HsmController(IHsmService hsm, ILogger<HsmController> logger)
    {
        _hsm = hsm;
        _logger = logger;
    }

    // GET /api/hsm/health - Returns service health and the number of stored keys.
    [AllowAnonymous]
    [HttpGet("health")]
    public IActionResult Health() => Ok(new
    {
        service = "PinEncryptionService",
        status = "Healthy",
        keyCount = _hsm.ListKeyIds().Count,
        timestamp = DateTime.UtcNow
    });

    // GET /api/hsm/keys - Lists all key IDs stored in the HSM key store.
    [HttpGet("keys")]
    public IActionResult ListKeys() => Ok(new { keyIds = _hsm.ListKeyIds() });

    // POST /api/hsm/keys/rotate - Rotates an existing key by generating a replacement.
    // Body: { "existingKeyId": "old-key", "newKeyId": "new-key" }
    [HttpPost("keys/rotate")]
    public IActionResult RotateKey([FromBody] RotateKeyRequest request)
    {
        try
        {
            var result = _hsm.RotateKey(request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    // POST /api/hsm/keys/generate - Generates a new cryptographic key.
    // Body: { "keyType": 0, "keyId": "my-zpk" }  (keyType: 0=ZMK, 1=ZPK, 2=PVK, 3=CVK, 4=TAK)
    [HttpPost("keys/generate")]
    public IActionResult GenerateKey([FromBody] GenerateKeyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.KeyId))
            return BadRequest(new { error = "KeyId is required." });

        if (_hsm.HasKey(request.KeyId))
            return Conflict(new { error = $"Key '{request.KeyId}' already exists." });

        var result = _hsm.GenerateKey(request);
        _logger.LogInformation("Generated key {KeyId} of type {KeyType}", result.KeyId, result.KeyType);
        return Ok(result);
    }

    // POST /api/hsm/pin/encrypt - Encrypts a cleartext PIN under a ZPK.
    // Body: { "pin": "1234", "pan": "4111111111111111", "zpkId": "default-zpk" }
    [HttpPost("pin/encrypt")]
    public IActionResult EncryptPin([FromBody] EncryptPinRequest request)
    {
        if (!_hsm.HasKey(request.ZpkId))
            return NotFound(new { error = $"ZPK '{request.ZpkId}' not found." });

        var result = _hsm.EncryptPin(request);
        return Ok(result);
    }

    // POST /api/hsm/pin/decrypt - Decrypts a PIN block back to cleartext.
    // Body: { "encryptedPinBlock": "AABB...", "pan": "4111111111111111", "zpkId": "default-zpk" }
    [HttpPost("pin/decrypt")]
    public IActionResult DecryptPin([FromBody] DecryptPinRequest request)
    {
        if (!_hsm.HasKey(request.ZpkId))
            return NotFound(new { error = $"ZPK '{request.ZpkId}' not found." });

        var result = _hsm.DecryptPin(request);
        return Ok(result);
    }

    // POST /api/hsm/pin/translate - Translates a PIN block from one ZPK to another.
    // Used when forwarding a transaction between two zones with different keys.
    // Body: { "encryptedPinBlock": "AABB...", "sourceZpkId": "zpk-1", "destZpkId": "zpk-2", "pan": "..." }
    [HttpPost("pin/translate")]
    public IActionResult TranslatePin([FromBody] TranslatePinRequest request)
    {
        if (!_hsm.HasKey(request.SourceZpkId))
            return NotFound(new { error = $"Source ZPK '{request.SourceZpkId}' not found." });

        if (!_hsm.HasKey(request.DestZpkId))
            return NotFound(new { error = $"Destination ZPK '{request.DestZpkId}' not found." });

        var result = _hsm.TranslatePin(request);
        return Ok(result);
    }

    // POST /api/hsm/pin/verify - Verifies a PIN block against an expected cleartext PIN.
    // Body: { "encryptedPinBlock": "AABB...", "pan": "...", "zpkId": "...", "expectedPin": "1234" }
    [HttpPost("pin/verify")]
    public IActionResult VerifyPin([FromBody] VerifyPinRequest request)
    {
        if (!_hsm.HasKey(request.ZpkId))
            return NotFound(new { error = $"ZPK '{request.ZpkId}' not found." });

        var result = _hsm.VerifyPin(request);
        return Ok(result);
    }
}
