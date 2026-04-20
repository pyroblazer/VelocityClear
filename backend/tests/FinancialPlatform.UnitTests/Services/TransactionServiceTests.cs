using FinancialPlatform.EventInfrastructure.Bus;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.TransactionService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TransactionAppService = FinancialPlatform.TransactionService.Services.TransactionService;

namespace FinancialPlatform.UnitTests.Services;

public class TransactionServiceTests
{
    private async Task<(TransactionAppService Service, TransactionDbContext Db, AdaptiveEventBus Bus)> CreateService()
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new TransactionDbContext(options);
        var bus = new AdaptiveEventBus(
            Mock.Of<ILogger<AdaptiveEventBus>>(),
            Mock.Of<ILogger<InMemoryEventBus>>(),
            Mock.Of<ILogger<RedisEventBus>>(),
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            Mock.Of<ILogger<KafkaEventBus>>());

        var service = new TransactionAppService(
            db, bus, Mock.Of<ILogger<TransactionAppService>>());

        return (service, db, bus);
    }

    [Fact]
    public async Task CreateTransactionAsync_ValidRequest_CreatesTransaction()
    {
        var (service, db, _) = await CreateService();
        var request = new CreateTransactionRequest("user_001", 100m, "USD", "Test payment", "Counterparty A");

        var result = await service.CreateTransactionAsync(request);

        Assert.NotNull(result);
        Assert.Equal("user_001", result.UserId);
        Assert.Equal(100m, result.Amount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal("Test payment", result.Description);
        Assert.Equal("Counterparty A", result.Counterparty);
        Assert.NotEmpty(result.Id);

        var fromDb = await db.Transactions.FindAsync(result.Id);
        Assert.NotNull(fromDb);
    }

    [Fact]
    public async Task CreateTransactionAsync_ZeroAmount_ThrowsArgumentException()
    {
        var (service, _, _) = await CreateService();
        var request = new CreateTransactionRequest("user_001", 0m, "USD", null, null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateTransactionAsync(request));
    }

    [Fact]
    public async Task CreateTransactionAsync_NegativeAmount_ThrowsArgumentException()
    {
        var (service, _, _) = await CreateService();
        var request = new CreateTransactionRequest("user_001", -50m, "USD", null, null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateTransactionAsync(request));
    }

    [Fact]
    public async Task CreateTransactionAsync_PublishesEvent()
    {
        var (service, _, bus) = await CreateService();
        TransactionCreatedEvent? captured = null;
        await bus.SubscribeAsync<TransactionCreatedEvent>(e => { captured = e; return Task.CompletedTask; });

        var request = new CreateTransactionRequest("user_001", 250m, "EUR", null, null);
        var result = await service.CreateTransactionAsync(request);

        Assert.NotNull(captured);
        Assert.Equal(result.Id, captured.TransactionId);
        Assert.Equal("user_001", captured.UserId);
        Assert.Equal(250m, captured.Amount);
        Assert.Equal("EUR", captured.Currency);
    }

    [Fact]
    public async Task CreateTransactionAsync_SetsStatusToPending()
    {
        var (service, _, _) = await CreateService();
        var request = new CreateTransactionRequest("user_001", 100m, "USD", null, null);
        var result = await service.CreateTransactionAsync(request);
        Assert.Equal("Pending", result.Status.ToString());
    }

    [Fact]
    public async Task CreateTransactionAsync_GeneratesUniqueId()
    {
        var (service, _, _) = await CreateService();
        var r1 = await service.CreateTransactionAsync(new CreateTransactionRequest("u1", 100m, "USD", null, null));
        var r2 = await service.CreateTransactionAsync(new CreateTransactionRequest("u2", 200m, "USD", null, null));
        Assert.NotEqual(r1.Id, r2.Id);
    }

    [Fact]
    public async Task CreateTransactionAsync_SetsTimestampToUtcNow()
    {
        var (service, _, _) = await CreateService();
        var before = DateTime.UtcNow.AddSeconds(-1);
        var result = await service.CreateTransactionAsync(new CreateTransactionRequest("u1", 100m, "USD", null, null));
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.True(result.Timestamp >= before && result.Timestamp <= after);
    }

    [Fact]
    public async Task GetTransactionAsync_ExistingId_ReturnsTransaction()
    {
        var (service, _, _) = await CreateService();
        var created = await service.CreateTransactionAsync(new CreateTransactionRequest("u1", 100m, "USD", null, null));
        var found = await service.GetTransactionAsync(created.Id);
        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public async Task GetTransactionAsync_NonexistentId_ReturnsNull()
    {
        var (service, _, _) = await CreateService();
        var found = await service.GetTransactionAsync("nonexistent");
        Assert.Null(found);
    }

    [Fact]
    public async Task GetAllTransactionsAsync_ReturnsAllOrderedByTimestamp()
    {
        var (service, _, _) = await CreateService();
        await service.CreateTransactionAsync(new CreateTransactionRequest("u1", 100m, "USD", null, null));
        await service.CreateTransactionAsync(new CreateTransactionRequest("u2", 200m, "USD", null, null));

        var all = await service.GetAllTransactionsAsync();
        var list = all.ToList();
        Assert.Equal(2, list.Count);
        Assert.True(list[0].Timestamp >= list[1].Timestamp);
    }

    [Fact]
    public async Task GetAllTransactionsAsync_EmptyDb_ReturnsEmpty()
    {
        var (service, _, _) = await CreateService();
        var all = await service.GetAllTransactionsAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task GetTransactionsByUserAsync_ReturnsOnlyUserTransactions()
    {
        var (service, _, _) = await CreateService();
        await service.CreateTransactionAsync(new CreateTransactionRequest("user_a", 100m, "USD", null, null));
        await service.CreateTransactionAsync(new CreateTransactionRequest("user_b", 200m, "USD", null, null));
        await service.CreateTransactionAsync(new CreateTransactionRequest("user_a", 300m, "EUR", null, null));

        var result = await service.GetTransactionsByUserAsync("user_a");
        var list = result.ToList();
        Assert.Equal(2, list.Count);
        Assert.All(list, t => Assert.Equal("user_a", t.UserId));
    }

    [Fact]
    public async Task GetTransactionsByUserAsync_UnknownUser_ReturnsEmpty()
    {
        var (service, _, _) = await CreateService();
        await service.CreateTransactionAsync(new CreateTransactionRequest("user_a", 100m, "USD", null, null));

        var result = await service.GetTransactionsByUserAsync("unknown");
        Assert.Empty(result);
    }
}
