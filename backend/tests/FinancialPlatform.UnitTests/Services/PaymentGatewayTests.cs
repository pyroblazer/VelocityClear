using FinancialPlatform.PaymentService.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class PaymentGatewayTests
{
    private readonly PaymentGateway _gateway;

    public PaymentGatewayTests()
    {
        var logger = new Mock<ILogger<PaymentGateway>>();
        _gateway = new PaymentGateway(logger.Object);
    }

    [Fact]
    public void Authorize_LowAmountLowRisk_ReturnsTrue()
    {
        var (authorized, reason) = _gateway.Authorize(100m, 10);
        Assert.True(authorized);
        Assert.Equal("Authorized", reason);
    }

    [Fact]
    public void Authorize_AmountExceedsLimit_ReturnsFalse()
    {
        var (authorized, reason) = _gateway.Authorize(60000m, 10);
        Assert.False(authorized);
        Assert.Equal("Amount exceeds daily limit", reason);
    }

    [Fact]
    public void Authorize_HighRiskScore_ReturnsFalse()
    {
        var (authorized, reason) = _gateway.Authorize(1000m, 85);
        Assert.False(authorized);
        Assert.Equal("Risk score too high", reason);
    }

    [Fact]
    public void Authorize_HighAmountWithElevatedRisk_ReturnsFalse()
    {
        var (authorized, reason) = _gateway.Authorize(6000m, 55);
        Assert.False(authorized);
        Assert.Equal("High amount with elevated risk", reason);
    }

    [Fact]
    public void Authorize_BoundaryAmount_ReturnsTrue()
    {
        var (authorized, reason) = _gateway.Authorize(4999.99m, 49);
        Assert.True(authorized);
        Assert.Equal("Authorized", reason);
    }

    [Fact]
    public void Authorize_ExactThreshold_Amount50000_ReturnsTrue()
    {
        var (authorized, _) = _gateway.Authorize(50000m, 10);
        Assert.True(authorized);
    }

    [Fact]
    public void Authorize_ExactThreshold_RiskScore80_ReturnsFalse()
    {
        var (authorized, _) = _gateway.Authorize(500m, 80);
        Assert.False(authorized);
    }

    [Fact]
    public void Authorize_ExactThreshold_Amount5000Risk50_ReturnsTrue()
    {
        var (authorized, _) = _gateway.Authorize(5000m, 50);
        Assert.True(authorized);
    }

    [Fact]
    public void Authorize_ZeroAmount_ReturnsTrue()
    {
        var (authorized, _) = _gateway.Authorize(0m, 10);
        Assert.True(authorized);
    }

    [Fact]
    public void Authorize_ZeroRisk_ReturnsTrue()
    {
        var (authorized, _) = _gateway.Authorize(1000m, 0);
        Assert.True(authorized);
    }
}
