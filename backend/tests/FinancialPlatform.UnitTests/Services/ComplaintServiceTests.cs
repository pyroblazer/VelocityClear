using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class ComplaintServiceTests
{
    private static ComplaintService CreateService(out ComplianceDbContext db)
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db = new ComplianceDbContext(options);
        return new ComplaintService(db, Mock.Of<ILogger<ComplaintService>>());
    }

    [Fact]
    public async Task CreateComplaint_HasSlaDeadline()
    {
        var svc = CreateService(out _);
        var result = await svc.CreateComplaintAsync(new CreateComplaintRequest(
            "user1", ComplaintCategory.Transaction, "My money is gone", "I made a payment but it didn't arrive", null));

        Assert.Equal(ComplaintStatus.Submitted, result.Status);
        Assert.True(result.SlaDeadline > DateTime.UtcNow);
    }

    [Fact]
    public async Task Acknowledge_SetsAcknowledgedStatus()
    {
        var svc = CreateService(out _);
        var complaint = await svc.CreateComplaintAsync(new CreateComplaintRequest(
            "user2", ComplaintCategory.FeeDispute, "Wrong fee", "Charged twice", null));

        var result = await svc.AcknowledgeAsync(complaint.Id);

        Assert.Equal(ComplaintStatus.Acknowledged, result!.Status);
    }

    [Fact]
    public async Task Resolve_SetsResolution()
    {
        var svc = CreateService(out _);
        var complaint = await svc.CreateComplaintAsync(new CreateComplaintRequest(
            "user3", ComplaintCategory.Other, "Issue", "Details", null));
        await svc.AcknowledgeAsync(complaint.Id);

        var result = await svc.ResolveAsync(complaint.Id,
            new ResolveComplaintRequest("Refund processed", "support-agent-1"));

        Assert.Equal(ComplaintStatus.Resolved, result!.Status);
        Assert.Equal("Refund processed", result.Resolution);
    }

    [Fact]
    public async Task Escalate_SetsEscalationLevel()
    {
        var svc = CreateService(out _);
        var complaint = await svc.CreateComplaintAsync(new CreateComplaintRequest(
            "user4", ComplaintCategory.UnauthorizedAccess, "Hacked", "Unauthorized transaction", "tx-abc"));

        var result = await svc.EscalateAsync(complaint.Id,
            new EscalateComplaintRequest(EscalationLevel.Level2, "Unresolved after 5 days"));

        Assert.Equal(EscalationLevel.Level2, result!.EscalationLevel);
        Assert.Equal(ComplaintStatus.Escalated, result.Status);
    }

    [Fact]
    public async Task GetNonExistent_ReturnsNull()
    {
        var svc = CreateService(out _);
        Assert.Null(await svc.GetComplaintAsync("nonexistent"));
    }
}
