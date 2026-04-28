using System.Security.Cryptography;
using System.Text;
using FinancialPlatform.ComplianceService.Data;
using Microsoft.EntityFrameworkCore;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Models;

namespace FinancialPlatform.ComplianceService.Services;

public class DigitalSignatureService
{
    private readonly ComplianceDbContext _db;
    private readonly ILogger<DigitalSignatureService> _logger;
    private readonly byte[] _signingKey;

    public DigitalSignatureService(ComplianceDbContext db, IConfiguration config, ILogger<DigitalSignatureService> logger)
    {
        _db = db;
        _logger = logger;
        var key = config["DigitalSignature:Key"] ?? "VelocityClear-DSig-Key-OJK-2024";
        _signingKey = Encoding.UTF8.GetBytes(key);
    }

    public async Task<SignatureResponse> SignDocumentAsync(SignDocumentRequest request)
    {
        var docHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.DocumentContent)));
        var signature = ComputeHmac(docHash + request.DocumentId + request.SignerId);

        var doc = new SignedDocument
        {
            DocumentId = request.DocumentId,
            SignerId = request.SignerId,
            Status = DocumentSigningStatus.Signed,
            Signature = signature,
            DocumentHash = docHash,
            SignedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddYears(5)
        };
        _db.SignedDocuments.Add(doc);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Document signed: {DocId} by {SignerId}", request.DocumentId, request.SignerId);
        return MapToResponse(doc);
    }

    public async Task<SignatureResponse?> VerifySignatureAsync(VerifySignatureRequest request)
    {
        var doc = await _db.SignedDocuments
            .Where(d => d.DocumentId == request.DocumentId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();

        if (doc == null) return null;

        var docHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.DocumentContent)));
        var expectedSig = ComputeHmac(docHash + request.DocumentId + doc.SignerId);
        var valid = expectedSig == request.Signature && docHash == doc.DocumentHash;

        doc.Status = valid ? DocumentSigningStatus.Verified : DocumentSigningStatus.Rejected;
        doc.VerifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapToResponse(doc);
    }

    public async Task<SignatureResponse?> GetSignatureAsync(string documentId)
    {
        var doc = await _db.SignedDocuments
            .Where(d => d.DocumentId == documentId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();
        return doc == null ? null : MapToResponse(doc);
    }

    private string ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(_signingKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
    }

    private static SignatureResponse MapToResponse(SignedDocument d) => new(
        d.Id, d.DocumentId, d.SignerId, d.Status,
        d.Signature, d.CreatedAt, d.SignedAt);
}
