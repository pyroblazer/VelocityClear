using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.ComplianceService.Services;
using FinancialPlatform.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class AuditServiceRealTests
{
    private async Task<(AuditService Service, ComplianceDbContext Db)> CreateService()
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new ComplianceDbContext(options);
        var sseHub = Mock.Of<ISseHub>();
        var service = new AuditService(db, sseHub, Mock.Of<ILogger<AuditService>>());
        return (service, db);
    }

    [Fact]
    public async Task LogEventAsync_CreatesAuditLog()
    {
        var (service, db) = await CreateService();
        await service.LogEventAsync("TransactionCreated", new { txId = "tx_001", amount = 100m });

        var logs = await db.AuditLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal("TransactionCreated", logs[0].EventType);
        Assert.NotNull(logs[0].Hash);
        Assert.Null(logs[0].PreviousHash);
    }

    [Fact]
    public async Task LogEventAsync_ChainsHashes()
    {
        var (service, db) = await CreateService();
        await service.LogEventAsync("Event1", new { data = "first" });
        await service.LogEventAsync("Event2", new { data = "second" });

        var logs = await db.AuditLogs.OrderBy(l => l.CreatedAt).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Null(logs[0].PreviousHash);
        Assert.Equal(logs[0].Hash, logs[1].PreviousHash);
    }

    [Fact]
    public async Task LogEventAsync_HashIs64CharHex()
    {
        var (service, db) = await CreateService();
        await service.LogEventAsync("Test", new { x = 1 });

        var log = await db.AuditLogs.FirstAsync();
        Assert.Equal(64, log.Hash.Length);
        Assert.True(log.Hash.All(c => char.IsLetterOrDigit(c)));
    }

    [Fact]
    public async Task LogEventAsync_DifferentPayloads_DifferentHashes()
    {
        var (service, db) = await CreateService();
        await service.LogEventAsync("Type", new { v = 1 });
        await service.LogEventAsync("Type", new { v = 2 });

        var logs = await db.AuditLogs.OrderBy(l => l.CreatedAt).ToListAsync();
        Assert.NotEqual(logs[0].Hash, logs[1].Hash);
    }

    [Fact]
    public async Task LogEventAsync_MultipleEvents_MaintainsChain()
    {
        var (service, db) = await CreateService();
        for (int i = 0; i < 5; i++)
        {
            await service.LogEventAsync($"Event{i}", new { index = i });
        }

        var logs = await db.AuditLogs.OrderBy(l => l.CreatedAt).ToListAsync();
        Assert.Equal(5, logs.Count);
        Assert.Null(logs[0].PreviousHash);
        for (int i = 1; i < logs.Count; i++)
        {
            Assert.Equal(logs[i - 1].Hash, logs[i].PreviousHash);
        }
    }

    [Fact]
    public async Task LogEventAsync_SetsCreatedAt()
    {
        var (service, db) = await CreateService();
        var before = DateTime.UtcNow.AddSeconds(-1);
        await service.LogEventAsync("Test", new { x = 1 });
        var after = DateTime.UtcNow.AddSeconds(1);

        var log = await db.AuditLogs.FirstAsync();
        Assert.True(log.CreatedAt >= before && log.CreatedAt <= after);
    }

    [Fact]
    public async Task LogEventAsync_PayloadContainsSerializedData()
    {
        var (service, db) = await CreateService();
        await service.LogEventAsync("Test", new { txId = "abc123" });

        var log = await db.AuditLogs.FirstAsync();
        Assert.Contains("abc123", log.Payload);
        Assert.Contains("txId", log.Payload);
    }
}
