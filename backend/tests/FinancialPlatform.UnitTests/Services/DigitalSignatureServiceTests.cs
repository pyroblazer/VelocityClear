using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class DigitalSignatureServiceTests
{
    private static DigitalSignatureService CreateService(out ComplianceDbContext db)
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db = new ComplianceDbContext(options);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DigitalSignature:Key"] = "TestKeyForVelocityClearOjkCompliance"
            })
            .Build();
        return new DigitalSignatureService(db, config, Mock.Of<ILogger<DigitalSignatureService>>());
    }

    [Fact]
    public async Task SignDocument_ReturnsSignedStatus()
    {
        var svc = CreateService(out _);
        var result = await svc.SignDocumentAsync(new SignDocumentRequest("doc1", "signer1", "Hello World"));

        Assert.Equal(DocumentSigningStatus.Signed, result.Status);
        Assert.NotNull(result.Signature);
    }

    [Fact]
    public async Task VerifySignature_ValidContent_ReturnsVerified()
    {
        var svc = CreateService(out _);
        var signed = await svc.SignDocumentAsync(new SignDocumentRequest("doc2", "signer2", "Test Content"));

        var result = await svc.VerifySignatureAsync(
            new VerifySignatureRequest("doc2", signed.Signature!, "Test Content"));

        Assert.NotNull(result);
        Assert.Equal(DocumentSigningStatus.Verified, result!.Status);
    }

    [Fact]
    public async Task VerifySignature_TamperedContent_ReturnsRejected()
    {
        var svc = CreateService(out _);
        var signed = await svc.SignDocumentAsync(new SignDocumentRequest("doc3", "signer3", "Original Content"));

        var result = await svc.VerifySignatureAsync(
            new VerifySignatureRequest("doc3", signed.Signature!, "Tampered Content"));

        Assert.Equal(DocumentSigningStatus.Rejected, result!.Status);
    }

    [Fact]
    public async Task GetSignature_ReturnsLatestForDocument()
    {
        var svc = CreateService(out _);
        await svc.SignDocumentAsync(new SignDocumentRequest("doc4", "signer4", "Some content"));

        var result = await svc.GetSignatureAsync("doc4");

        Assert.NotNull(result);
        Assert.Equal("doc4", result!.DocumentId);
    }
}
