using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class SocServiceTests
{
    private static SocService CreateService(out ComplianceDbContext db)
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db = new ComplianceDbContext(options);
        return new SocService(db, Mock.Of<ILogger<SocService>>());
    }

    [Fact]
    public async Task CreateIncident_DefaultStatusIsDetected()
    {
        var svc = CreateService(out _);
        var result = await svc.CreateIncidentAsync(new CreateIncidentRequest(
            "SQL Injection Attempt", "Suspicious queries detected", IncidentSeverity.High, "api-gateway", null));

        Assert.Equal(IncidentStatus.Detected, result.Status);
        Assert.Equal(IncidentSeverity.High, result.Severity);
    }

    [Fact]
    public async Task UpdateIncident_Resolved_SetsResolvedAt()
    {
        var svc = CreateService(out var db);
        var incident = await svc.CreateIncidentAsync(new CreateIncidentRequest(
            "Brute Force", "Login attempts", IncidentSeverity.Medium, "auth-service", "RB-001"));

        await svc.UpdateIncidentAsync(incident.Id, new UpdateIncidentRequest(
            IncidentStatus.Resolved, "Blocked IP range", "Attacker from single IP", null));

        var updated = await db.SecurityIncidents.FindAsync(incident.Id);
        Assert.Equal(IncidentStatus.Resolved, updated!.Status);
        Assert.NotNull(updated.ResolvedAt);
    }

    [Fact]
    public async Task Dashboard_CountsCorrectly()
    {
        var svc = CreateService(out _);
        await svc.CreateIncidentAsync(new CreateIncidentRequest("I1", "D1", IncidentSeverity.Critical, null, null));
        await svc.CreateIncidentAsync(new CreateIncidentRequest("I2", "D2", IncidentSeverity.High, null, null));

        var dashboard = await svc.GetDashboardAsync();

        Assert.Equal(2, dashboard.OpenIncidents);
        Assert.Equal(1, dashboard.CriticalIncidents);
    }

    [Fact]
    public async Task ListIncidents_FilterByStatus()
    {
        var svc = CreateService(out _);
        var i = await svc.CreateIncidentAsync(new CreateIncidentRequest("I3", "D3", IncidentSeverity.Low, null, null));
        await svc.UpdateIncidentAsync(i.Id, new UpdateIncidentRequest(IncidentStatus.Closed, null, null, null));

        var open = await svc.ListIncidentsAsync(IncidentStatus.Detected);
        Assert.Empty(open);

        var closed = await svc.ListIncidentsAsync(IncidentStatus.Closed);
        Assert.Single(closed);
    }
}
