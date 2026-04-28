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

public class ApprovalServiceTests
{
    private static ApprovalService CreateService(out ComplianceDbContext db)
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db = new ComplianceDbContext(options);
        return new ApprovalService(db, Mock.Of<IEventBus>(), Mock.Of<ILogger<ApprovalService>>());
    }

    [Fact]
    public async Task CreateApproval_StatusIsPending()
    {
        var svc = CreateService(out _);
        var result = await svc.CreateApprovalAsync(new CreateApprovalRequest(
            ApprovalType.RoleChange, "maker1", "{}", null, null, null));

        Assert.Equal(ApprovalStatus.PendingApproval, result.Status);
        Assert.Equal("maker1", result.RequestedBy);
    }

    [Fact]
    public async Task ProcessApproval_Approved_UpdatesStatus()
    {
        var svc = CreateService(out _);
        var approval = await svc.CreateApprovalAsync(new CreateApprovalRequest(
            ApprovalType.UserCreation, "maker1", "{}", null, null, null));

        var result = await svc.ProcessApprovalAsync(approval.Id,
            new ProcessApprovalRequest("checker1", true, "Looks good", null));

        Assert.Equal(ApprovalStatus.Approved, result.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    [Fact]
    public async Task ProcessApproval_SameMakerChecker_Throws()
    {
        var svc = CreateService(out _);
        var approval = await svc.CreateApprovalAsync(new CreateApprovalRequest(
            ApprovalType.SarFiling, "alice", "{}", null, null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ProcessApprovalAsync(approval.Id,
                new ProcessApprovalRequest("alice", true, null, null)));
    }

    [Fact]
    public async Task ProcessApproval_Rejected_SetsRejectedStatus()
    {
        var svc = CreateService(out _);
        var approval = await svc.CreateApprovalAsync(new CreateApprovalRequest(
            ApprovalType.ConfigurationChange, "maker2", "{}", null, null, null));

        var result = await svc.ProcessApprovalAsync(approval.Id,
            new ProcessApprovalRequest("checker2", false, "Policy violation", null));

        Assert.Equal(ApprovalStatus.Rejected, result.Status);
        Assert.Equal("Policy violation", result.Comments);
    }

    [Fact]
    public async Task ProcessAlreadyProcessed_Throws()
    {
        var svc = CreateService(out _);
        var approval = await svc.CreateApprovalAsync(new CreateApprovalRequest(
            ApprovalType.UserCreation, "m1", "{}", null, null, null));
        await svc.ProcessApprovalAsync(approval.Id, new ProcessApprovalRequest("c1", true, null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ProcessApprovalAsync(approval.Id, new ProcessApprovalRequest("c2", true, null, null)));
    }
}
