using System;
using BCrypt.Net;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class BcryptPasswordTests
{
    [Fact]
    public void HashPassword_ProducesValidBcryptHash()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("mypassword", workFactor: 12);

        Assert.True(hash.StartsWith("$2a$") || hash.StartsWith("$2b$"));
        Assert.True(hash.Length > 50);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("admin123", workFactor: 12);

        Assert.True(BCrypt.Net.BCrypt.Verify("admin123", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("admin123", workFactor: 12);

        Assert.False(BCrypt.Net.BCrypt.Verify("wrongpassword", hash));
    }

    [Fact]
    public void HashPassword_DifferentHashesForSamePassword()
    {
        var hash1 = BCrypt.Net.BCrypt.HashPassword("test123", workFactor: 12);
        var hash2 = BCrypt.Net.BCrypt.HashPassword("test123", workFactor: 12);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void SeededHashes_VerifyAgainstKnownPasswords()
    {
        Assert.True(BCrypt.Net.BCrypt.Verify("admin123",
            "$2a$12$/lQtv3vrRF.QqWmY1OEsFOtA7GhhDQEo19hrSfk.vfOQdjI0m.HP6"));
        Assert.True(BCrypt.Net.BCrypt.Verify("trader123",
            "$2a$12$3BAiQTMvl39DdCAYLPyhTOlW7EvlLOi6DQG.vt88GXl80q7kCK9nG"));
        Assert.True(BCrypt.Net.BCrypt.Verify("auditor123",
            "$2a$12$DrG9BY1nNKlf3aAOmTBUle2LCymMHolvhutRiWXYzspxyY9EHm8jG"));
        Assert.True(BCrypt.Net.BCrypt.Verify("test123",
            "$2a$12$KMyixRvyieKj/z283wQfLOJg.GfSzNGSR4FxOys.XljRhQYIN2Owy"));
    }
}
