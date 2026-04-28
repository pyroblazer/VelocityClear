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

public class ConsentServiceTests
{
    private static ConsentService CreateService(out ComplianceDbContext db)
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db = new ComplianceDbContext(options);
        return new ConsentService(db, Mock.Of<IEventBus>(), Mock.Of<ILogger<ConsentService>>());
    }

    [Fact]
    public async Task GrantConsent_CreatesRecord()
    {
        var svc = CreateService(out var db);
        var req = new GrantConsentRequest("user1", ConsentType.DataProcessing, "127.0.0.1", null, "POJK");

        var result = await svc.GrantConsentAsync(req);

        Assert.Equal(ConsentStatus.Granted, result.Status);
        Assert.Single(await db.ConsentRecords.ToListAsync());
    }

    [Fact]
    public async Task GrantConsent_Idempotent_DoesNotDuplicate()
    {
        var svc = CreateService(out var db);
        var req = new GrantConsentRequest("user1", ConsentType.Marketing, null, null, null);

        await svc.GrantConsentAsync(req);
        await svc.GrantConsentAsync(req);

        Assert.Single(await db.ConsentRecords.ToListAsync());
    }

    [Fact]
    public async Task WithdrawConsent_SetsWithdrawnStatus()
    {
        var svc = CreateService(out _);
        await svc.GrantConsentAsync(new GrantConsentRequest("user2", ConsentType.Marketing, null, null, null));

        var result = await svc.WithdrawConsentAsync(new WithdrawConsentRequest("user2", ConsentType.Marketing, "User request"));

        Assert.Equal(ConsentStatus.Withdrawn, result.Status);
        Assert.NotNull(result.WithdrawnAt);
    }

    [Fact]
    public async Task WithdrawConsent_NoActiveConsent_Throws()
    {
        var svc = CreateService(out _);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.WithdrawConsentAsync(new WithdrawConsentRequest("ghost", ConsentType.Marketing, null)));
    }

    [Fact]
    public async Task CheckConsent_NoConsent_ReturnsFalse()
    {
        var svc = CreateService(out _);
        var result = await svc.CheckConsentAsync("newuser", ConsentType.DataProcessing);
        Assert.False(result.HasActiveConsent);
    }

    [Fact]
    public async Task CheckConsent_AfterGrant_ReturnsTrue()
    {
        var svc = CreateService(out _);
        await svc.GrantConsentAsync(new GrantConsentRequest("user3", ConsentType.DataProcessing, null, null, null));

        var result = await svc.CheckConsentAsync("user3", ConsentType.DataProcessing);
        Assert.True(result.HasActiveConsent);
    }
}
