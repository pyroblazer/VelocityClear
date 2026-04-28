using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class SarServiceTests
{
    private static SarService CreateService(out ComplianceDbContext db)
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db = new ComplianceDbContext(options);
        return new SarService(db, Mock.Of<IEventBus>(), Mock.Of<ILogger<SarService>>());
    }

    [Fact]
    public async Task FileSar_AssignsOjkReference()
    {
        var svc = CreateService(out _);
        var req = new SarFilingRequest("tx1", "user1", "Suspicious structuring", 9500m, "Multiple just-below-threshold transactions", "officer1");

        var result = await svc.FileSarAsync(req);

        Assert.Equal(SarStatus.Filed, result.Status);
        Assert.NotNull(result.OjkReferenceNumber);
        Assert.StartsWith("SAR-", result.OjkReferenceNumber);
    }

    [Fact]
    public async Task UpdateStatus_AcknowledgedSetsReviewedAt()
    {
        var svc = CreateService(out var db);
        var req = new SarFilingRequest("tx2", "user2", "Narrative", 50000m, "Large wire", "officer2");
        var sar = await svc.FileSarAsync(req);

        await svc.UpdateSarStatusAsync(sar.Id, SarStatus.Acknowledged, "OJK confirmed receipt");

        var updated = await db.SuspiciousActivityReports.FindAsync(sar.Id);
        Assert.Equal(SarStatus.Acknowledged, updated!.Status);
        Assert.NotNull(updated.ReviewedAt);
    }

    [Fact]
    public async Task ListSars_FilterByStatus()
    {
        var svc = CreateService(out _);
        await svc.FileSarAsync(new SarFilingRequest("tx3", "u3", "N1", 1000m, "B1", "o1"));
        await svc.FileSarAsync(new SarFilingRequest("tx4", "u4", "N2", 2000m, "B2", "o2"));

        var filed = await svc.ListSarsAsync(SarStatus.Filed);
        Assert.Equal(2, filed.Count());
    }
}
