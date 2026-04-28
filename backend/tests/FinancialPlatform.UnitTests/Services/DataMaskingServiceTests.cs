using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class DataMaskingServiceTests
{
    private static DataMaskingService CreateService()
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DataMaskingService(new ComplianceDbContext(options));
    }

    [Fact]
    public void MaskFull_ReturnsAllAsterisks()
    {
        var svc = CreateService();
        var result = svc.MaskValue(new MaskDataRequest("SecretValue", "Full", DataClassificationLevel.Restricted));
        Assert.Equal("***********", result.MaskedValue);
    }

    [Fact]
    public void MaskLastFour_PreservesLastFourDigits()
    {
        var svc = CreateService();
        var result = svc.MaskValue(new MaskDataRequest("1234567890", "LastFour", DataClassificationLevel.PII));
        Assert.EndsWith("7890", result.MaskedValue);
        Assert.StartsWith("******", result.MaskedValue);
    }

    [Fact]
    public void MaskEmail_PreservesFirstTwoCharsAndDomain()
    {
        var svc = CreateService();
        var result = svc.MaskValue(new MaskDataRequest("john.doe@example.com", "Email", DataClassificationLevel.PII));
        Assert.StartsWith("jo", result.MaskedValue);
        Assert.Contains("@example.com", result.MaskedValue);
    }

    [Fact]
    public void MaskPhone_PreservesLastFourDigits()
    {
        var svc = CreateService();
        var result = svc.MaskValue(new MaskDataRequest("081234567890", "Phone", DataClassificationLevel.PII));
        Assert.EndsWith("7890", result.MaskedValue);
    }

    [Fact]
    public void MaskPartial_PreservesBothEnds()
    {
        var svc = CreateService();
        var result = svc.MaskValue(new MaskDataRequest("1234567890AB", "Partial", DataClassificationLevel.Confidential));
        Assert.NotEqual("1234567890AB", result.MaskedValue);
        Assert.Contains("*", result.MaskedValue);
    }

    [Fact]
    public void MaskUnknownRule_DefaultsToFull()
    {
        var svc = CreateService();
        var result = svc.MaskValue(new MaskDataRequest("Test", "Unknown", DataClassificationLevel.Restricted));
        Assert.Equal("****", result.MaskedValue);
    }
}
