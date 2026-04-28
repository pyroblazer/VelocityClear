using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.ComplianceService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class InfrastructureComplianceServiceTests
{
    private static InfrastructureComplianceService CreateService(out ComplianceDbContext db)
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db = new ComplianceDbContext(options);
        return new InfrastructureComplianceService(db, Mock.Of<ILogger<InfrastructureComplianceService>>());
    }

    [Fact]
    public async Task UpsertDrpPlan_CreatesNewPlan()
    {
        var svc = CreateService(out var db);
        var result = await svc.UpsertDrpPlanAsync("Main DR Plan", 60, 15);

        Assert.Equal("Main DR Plan", result.PlanName);
        Assert.Equal(60, result.RtoMinutes);
        Assert.Single(await db.DrpBcpStatuses.ToListAsync());
    }

    [Fact]
    public async Task UpsertDrpPlan_UpdatesExisting()
    {
        var svc = CreateService(out _);
        await svc.UpsertDrpPlanAsync("Plan A", 120, 30);
        var result = await svc.UpsertDrpPlanAsync("Plan A", 60, 15);

        Assert.Equal(60, result.RtoMinutes);
    }

    [Fact]
    public async Task RecordResidencyCheck_Compliant()
    {
        var svc = CreateService(out _);
        var result = await svc.RecordResidencyCheckAsync("transaction-service", "id-central-1", true);

        Assert.True(result.IsCompliant);
        Assert.Equal("id-central-1", result.Region);
    }

    [Fact]
    public async Task RecordVendorAudit_SlaMet_WhenUptimeExceedsTarget()
    {
        var svc = CreateService(out _);
        var now = DateTime.UtcNow;
        var result = await svc.RecordVendorAuditAsync("PrivyID", "eKYC", 99.5, 99.0, 0, now.AddDays(-30), now);

        Assert.True(result.SlaMet);
    }

    [Fact]
    public async Task RecordVendorAudit_SlaNotMet_WhenUptimeBelowTarget()
    {
        var svc = CreateService(out _);
        var now = DateTime.UtcNow;
        var result = await svc.RecordVendorAuditAsync("SomePSP", "Payment", 97.0, 99.0, 5, now.AddDays(-30), now);

        Assert.False(result.SlaMet);
    }
}
