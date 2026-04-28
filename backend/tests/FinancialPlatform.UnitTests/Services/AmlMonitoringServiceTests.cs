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

public class AmlMonitoringServiceTests
{
    private static AmlMonitoringService CreateService(out ComplianceDbContext db)
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db = new ComplianceDbContext(options);
        return new AmlMonitoringService(db, Mock.Of<IEventBus>(), Mock.Of<ILogger<AmlMonitoringService>>());
    }

    [Fact]
    public async Task CreateAlert_PersistsAlert()
    {
        var svc = CreateService(out var db);
        await svc.CreateAlertAsync("tx1", "user1", "STRUCTURING", AlertSeverity.High, 9500m, "IDR");

        var alerts = await svc.ListAlertsAsync();
        Assert.Single(alerts);
        Assert.Equal("STRUCTURING", alerts.First().RuleTriggered);
    }

    [Fact]
    public async Task ResolveAlert_UpdatesStatus()
    {
        var svc = CreateService(out _);
        var alert = await svc.CreateAlertAsync("tx2", "user2", "ROUND_AMOUNT", AlertSeverity.Low, 5000m, "IDR");

        var result = await svc.ResolveAlertAsync(alert.Id,
            new ResolveAlertRequest("False positive — salary payment", "officer1", AlertStatus.FalsePositive));

        Assert.NotNull(result);
        Assert.Equal(AlertStatus.FalsePositive, result!.Status);
    }

    [Fact]
    public async Task ListAlerts_FilterByStatus()
    {
        var svc = CreateService(out _);
        await svc.CreateAlertAsync("tx3", "user3", "VELOCITY_24H", AlertSeverity.High, 1000m, "IDR");
        await svc.CreateAlertAsync("tx4", "user4", "CROSS_BORDER", AlertSeverity.Medium, 20000m, "USD");

        var openAlerts = await svc.ListAlertsAsync(AlertStatus.Open);
        Assert.Equal(2, openAlerts.Count());
    }
}
