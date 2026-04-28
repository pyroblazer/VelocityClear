using FinancialPlatform.ComplianceService.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class WormStorageServiceTests
{
    private static WormStorageService CreateService() =>
        new WormStorageService(Mock.Of<ILogger<WormStorageService>>());

    [Fact]
    public void Store_ThenVerify_Succeeds()
    {
        var worm = CreateService();
        worm.Store("key1", "content");
        Assert.True(worm.Verify("key1", "content"));
    }

    [Fact]
    public void Verify_TamperedContent_Fails()
    {
        var worm = CreateService();
        worm.Store("key2", "original content");
        Assert.False(worm.Verify("key2", "tampered content"));
    }

    [Fact]
    public void Store_DuplicateKey_ThrowsWormViolation()
    {
        var worm = CreateService();
        worm.Store("key3", "content");
        var ex = Assert.Throws<InvalidOperationException>(() => worm.Store("key3", "other"));
        Assert.Contains("WORM violation", ex.Message);
    }

    [Fact]
    public void Verify_NonExistentKey_ReturnsFalse()
    {
        var worm = CreateService();
        Assert.False(worm.Verify("missing", "content"));
    }

    [Fact]
    public void Exists_AfterStore_ReturnsTrue()
    {
        var worm = CreateService();
        worm.Store("key4", "data");
        Assert.True(worm.Exists("key4"));
    }

    [Fact]
    public void Count_ReflectsStoredEntries()
    {
        var worm = CreateService();
        Assert.Equal(0, worm.Count);
        worm.Store("a", "1");
        worm.Store("b", "2");
        Assert.Equal(2, worm.Count);
    }
}
