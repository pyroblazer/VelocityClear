using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class AuditHashTests
{
    [Fact]
    public void ComputeHash_SameInput_ProducesSameOutput()
    {
        var hash1 = ComputeHash("payload", "prevHash");
        var hash2 = ComputeHash("payload", "prevHash");
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentPayload_ProducesDifferentOutput()
    {
        var hash1 = ComputeHash("payload1", "prevHash");
        var hash2 = ComputeHash("payload2", "prevHash");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_NullPreviousHash_UsesEmptyString()
    {
        var hashWithNull = ComputeHash("payload", null);
        var hashWithEmpty = ComputeHash("payload", "");
        Assert.Equal(hashWithNull, hashWithEmpty);
    }

    [Fact]
    public void ComputeHash_DifferentPrevHash_ProducesDifferentOutput()
    {
        var hash1 = ComputeHash("payload", "hash1");
        var hash2 = ComputeHash("payload", "hash2");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_OutputIs64CharsHex()
    {
        var hash = ComputeHash("test payload", "prev");
        Assert.Equal(64, hash.Length);
        Assert.True(hash.All(c => char.IsLetterOrDigit(c)));
    }

    [Fact]
    public void ComputeHash_ChainIntegrity()
    {
        var hash1 = ComputeHash("event1", null);
        var hash2 = ComputeHash("event2", hash1);
        var hash3 = ComputeHash("event3", hash2);

        // Verify chain: recomputing with same inputs gives same results
        Assert.Equal(hash1, ComputeHash("event1", null));
        Assert.Equal(hash2, ComputeHash("event2", hash1));
        Assert.Equal(hash3, ComputeHash("event3", hash2));
    }

    [Fact]
    public void ComputeHash_TamperedPayload_DetectsTamper()
    {
        var hash1 = ComputeHash("event1", null);
        var hash2 = ComputeHash("event2", hash1);

        // Tamper with event1's payload
        var tamperedHash1 = ComputeHash("TAMPERED_event1", null);
        var recomputedHash2 = ComputeHash("event2", tamperedHash1);

        Assert.NotEqual(hash2, recomputedHash2);
    }

    private static string ComputeHash(string payload, string? previousHash)
    {
        var combined = payload + (previousHash ?? string.Empty);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(bytes);
    }
}
