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

public class KycServiceTests
{
    private static KycService CreateService(out ComplianceDbContext db)
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db = new ComplianceDbContext(options);
        return new KycService(db, Mock.Of<IEventBus>(), Mock.Of<ILogger<KycService>>());
    }

    [Fact]
    public async Task InitiateKyc_CreatesProfile_InProgress()
    {
        var svc = CreateService(out var db);
        var req = new InitiateKycRequest("user1", "John Doe", "ID123456", "KTP", null);

        var result = await svc.InitiateKycAsync(req);

        Assert.Equal(KycStatus.InProgress, result.Status);
        Assert.Equal("user1", result.UserId);
        Assert.Single(await db.KycProfiles.ToListAsync());
    }

    [Fact]
    public async Task InitiateKyc_Idempotent_ReturnsExisting()
    {
        var svc = CreateService(out var db);
        var req = new InitiateKycRequest("user1", "John Doe", "ID123456", "KTP", null);

        await svc.InitiateKycAsync(req);
        await svc.InitiateKycAsync(req);

        Assert.Single(await db.KycProfiles.ToListAsync());
    }

    [Fact]
    public async Task LivenessCheck_SetsLivenessChecked()
    {
        var svc = CreateService(out _);
        var profile = await svc.InitiateKycAsync(new InitiateKycRequest("user2", "Jane", "ID999", "KTP", null));

        var result = await svc.PerformLivenessCheckAsync(new LivenessCheckRequest(profile.Id, "user2"));

        Assert.True(result.Confidence is >= 0.85 and <= 1.0);
        Assert.Equal(profile.Id, result.KycProfileId);
    }

    [Fact]
    public async Task LivenessCheck_UnknownProfile_Throws()
    {
        var svc = CreateService(out _);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.PerformLivenessCheckAsync(new LivenessCheckRequest("nonexistent", "user")));
    }

    [Fact]
    public async Task UpdateStatus_ToVerified_SetsVerifiedAt()
    {
        var svc = CreateService(out _);
        var profile = await svc.InitiateKycAsync(new InitiateKycRequest("user3", "Test", "ID000", "KTP", null));

        var result = await svc.UpdateStatusAsync(profile.Id, new UpdateKycStatusRequest(KycStatus.Verified, null));

        Assert.Equal(KycStatus.Verified, result.Status);
        Assert.NotNull(result.VerifiedAt);
        Assert.NotNull(result.ExpiresAt);
    }

    [Fact]
    public async Task IsUserVerified_ReturnsFalse_WhenNoVerifiedProfile()
    {
        var svc = CreateService(out _);
        Assert.False(await svc.IsUserVerifiedAsync("ghost-user"));
    }

    [Fact]
    public async Task IsUserVerified_ReturnsTrue_AfterVerification()
    {
        var svc = CreateService(out _);
        var profile = await svc.InitiateKycAsync(new InitiateKycRequest("user4", "Test", "ID555", "KTP", null));
        await svc.UpdateStatusAsync(profile.Id, new UpdateKycStatusRequest(KycStatus.Verified, null));

        Assert.True(await svc.IsUserVerifiedAsync("user4"));
    }

    [Fact]
    public async Task WatchlistScreen_NoHits_ReturnsFalse()
    {
        var svc = CreateService(out _);
        var profile = await svc.InitiateKycAsync(new InitiateKycRequest("user5", "Random Name", "ID777", "KTP", null));

        var result = await svc.ScreenWatchlistAsync(new WatchlistScreenRequest(profile.Id, "Random Name XYZ QWERTY", null));

        Assert.False(result.HitFound);
    }
}
