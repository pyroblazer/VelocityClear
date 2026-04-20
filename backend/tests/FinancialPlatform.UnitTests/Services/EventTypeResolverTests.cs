using FinancialPlatform.EventInfrastructure.Serialization;
using FinancialPlatform.Shared.Events;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class EventTypeResolverTests
{
    [Theory]
    [InlineData(nameof(TransactionCreatedEvent), typeof(TransactionCreatedEvent))]
    [InlineData(nameof(RiskEvaluatedEvent), typeof(RiskEvaluatedEvent))]
    [InlineData(nameof(PaymentAuthorizedEvent), typeof(PaymentAuthorizedEvent))]
    [InlineData(nameof(AuditLoggedEvent), typeof(AuditLoggedEvent))]
    public void Resolve_KnownTypes_ReturnsCorrectType(string typeName, Type expected)
    {
        var result = EventTypeResolver.Resolve(typeName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Resolve_UnknownType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => EventTypeResolver.Resolve("NonExistentEvent"));
    }
}
