using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FinancialPlatform.ApiGateway.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FinancialPlatform.UnitTests.Services;

public class JwtServiceTests
{
    private readonly JwtService _jwtService;

    public JwtServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Jwt:SecretKey", "TestSecretKey_MustBe32CharsOrMore!!"),
                new KeyValuePair<string, string?>("Jwt:Issuer", "TestIssuer")
            })
            .Build();

        _jwtService = new JwtService(config);
    }

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var token = _jwtService.GenerateToken("user_001", "Admin");
        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public void GenerateToken_ContainsCorrectSubject()
    {
        var token = _jwtService.GenerateToken("user_001", "Admin");
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("user_001", jwt.Subject);
    }

    [Fact]
    public void GenerateToken_ContainsRole()
    {
        var token = _jwtService.GenerateToken("user_001", "Admin");
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void GenerateToken_HasExpiryInFuture()
    {
        var token = _jwtService.GenerateToken("user_001", "User");
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }

    [Fact]
    public void GenerateToken_HasCorrectIssuer()
    {
        var token = _jwtService.GenerateToken("user_001", "User");
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("TestIssuer", jwt.Issuer);
    }

    [Fact]
    public void GenerateToken_DifferentCalls_ProduceDifferentTokens()
    {
        var token1 = _jwtService.GenerateToken("user_001", "Admin");
        var token2 = _jwtService.GenerateToken("user_001", "Admin");

        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void GenerateToken_CanBeValidated()
    {
        var token = _jwtService.GenerateToken("user_001", "Admin");

        var key = Encoding.UTF8.GetBytes("TestSecretKey_MustBe32CharsOrMore!!");
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "TestIssuer",
            ValidAudience = "TestIssuer",
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };

        var principal = handler.ValidateToken(token, parameters, out var validatedToken);
        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);
    }

    [Fact]
    public void Constructor_ThrowsWhenSecretKeyMissing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Assert.Throws<InvalidOperationException>(() => new JwtService(config));
    }

    [Fact]
    public void Constructor_ThrowsWhenSecretKeyTooShort()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Jwt:SecretKey", "too-short")
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new JwtService(config));
    }
}
