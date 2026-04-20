// ============================================================================
// PinEncryptionServiceTests.cs - Integration Tests for PIN Encryption Service
// ============================================================================
// These are "integration tests" - they start the entire web application in-memory
// and make real HTTP requests to it. This tests the full stack: controllers,
// DI container, middleware, serialization, and service logic all working together.
//
// Key C# / ASP.NET Core testing concepts:
//   WebApplicationFactory<TestEntry> - Creates an in-memory test server. This is
//     ASP.NET Core's built-in integration testing tool. It boots the entire app
//     (Program.cs, DI, middleware, controllers) but replaces the HTTP listener
//     with an in-memory transport (no real TCP socket).
//   TestEntry - A marker class in the PinEncryptionService project that tells
//     WebApplicationFactory which Program.cs to use.
//   HttpClient - Makes HTTP requests to the in-memory server. Created by the
//     factory, not by you (it automatically uses the correct base URL).
//   ReadFromJsonAsync<T>() - Deserializes an HTTP response body into a C# object.
//   PostAsJsonAsync(url, body) - Serializes a C# object to JSON and POSTs it.
//   HttpStatusCode - Enum of HTTP status codes (OK=200, NotFound=404, etc.).
//   record - A lightweight immutable data type used here to define response shapes
//     for deserialization. Similar to interfaces in TypeScript.
//   IDisposable - Ensures the test server is shut down after each test class run.
//   "new { KeyType = 0, KeyId = '...' }" - Anonymous type (like a JS object literal).
//     Used to create request bodies without defining a class.
// ============================================================================

using System.Net;
using System.Net.Http.Json;
using FinancialPlatform.ApiGateway.Services;
using FinancialPlatform.PinEncryptionService;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FinancialPlatform.IntegrationTests;

// Implements IDisposable so the test server is cleaned up after all tests in this class run.
// xUnit creates one instance of the class and shares it across all test methods (fixture pattern).
public class PinEncryptionServiceTests : IDisposable
{
    private const string JwtSecretKey = "TestSecretKey_MustBe32CharsOrMore!!";
    private const string JwtIssuer = "FinancialPlatform";

    // WebApplicationFactory boots the real app in-memory for testing.
    private readonly WebApplicationFactory<TestEntry> _factory;
    private readonly HttpClient _client;

    public PinEncryptionServiceTests()
    {
        // Create the test server. WithWebHostBuilder lets us override configuration.
        _factory = new WebApplicationFactory<TestEntry>()
            .WithWebHostBuilder(builder =>
            {
                // Use InMemory event bus so tests don't need Redis/RabbitMQ/Kafka.
                builder.UseSetting("EventBus:DefaultBackend", "InMemory");
                builder.UseSetting("Jwt:SecretKey", JwtSecretKey);
                builder.UseSetting("Jwt:Issuer", JwtIssuer);
            });
        // CreateClient() returns an HttpClient pre-configured to talk to the test server.
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateTestToken());
    }

    private static string GenerateTestToken(string userId = "test-user", string role = "Admin")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Jwt:SecretKey", JwtSecretKey),
                new KeyValuePair<string, string?>("Jwt:Issuer", JwtIssuer)
            })
            .Build();
        var jwtService = new JwtService(config);
        return jwtService.GenerateToken(userId, role);
    }

    // ---------------------------------------------------------------
    // HSM Health & Key Management Tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // GET /api/hsm/health should return 200 with service status info.
        var response = await _client.GetAsync("/api/hsm/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // ReadFromJsonAsync<HsmHealthResult>() deserializes the JSON response into
        // a typed object. The HsmHealthResult record (defined at the bottom of this
        // file) tells the deserializer what fields to expect.
        var result = await response.Content.ReadFromJsonAsync<HsmHealthResult>();
        Assert.NotNull(result);
        Assert.Equal("PinEncryptionService", result.Service);
        Assert.Equal("Healthy", result.Status);
    }

    [Fact]
    public async Task ListKeys_ReturnsDefaultZpk()
    {
        // The HSM seeds a "default-zpk" key on startup. Listing keys should include it.
        var response = await _client.GetAsync("/api/hsm/keys");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<KeyListResult>();
        Assert.NotNull(result);
        Assert.Contains("default-zpk", result.KeyIds);
    }

    [Fact]
    public async Task GenerateKey_ReturnsNewKey()
    {
        // POST /api/hsm/keys/generate with KeyType=0 (ZPK) and a unique KeyId.
        // "new { KeyType = 0, KeyId = ... }" creates an anonymous type (like a JS object).
        var response = await _client.PostAsJsonAsync("/api/hsm/keys/generate",
            new { KeyType = 0, KeyId = "test-zpk-001" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<GenerateKeyResult>();
        Assert.NotNull(result);
        Assert.Equal("test-zpk-001", result.KeyId);
        Assert.False(string.IsNullOrEmpty(result.KeyCheckValue));
    }

    [Fact]
    public async Task GenerateKey_DuplicateId_ReturnsConflict()
    {
        // Generating a key with the same ID twice should return 409 Conflict.
        await _client.PostAsJsonAsync("/api/hsm/keys/generate",
            new { KeyType = 0, KeyId = "dup-key" });
        var response = await _client.PostAsJsonAsync("/api/hsm/keys/generate",
            new { KeyType = 0, KeyId = "dup-key" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GenerateKey_EmptyKeyId_ReturnsBadRequest()
    {
        // Generating a key with an empty ID should return 400 Bad Request.
        var response = await _client.PostAsJsonAsync("/api/hsm/keys/generate",
            new { KeyType = 0, KeyId = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------
    // PIN Operation Tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task EncryptDecryptPin_RoundTrip()
    {
        // Encrypt a PIN, then decrypt the encrypted block - should get the original PIN.
        var encResponse = await _client.PostAsJsonAsync("/api/hsm/pin/encrypt",
            new { Pin = "1234", Pan = "4111111111111111", ZpkId = "default-zpk" });
        Assert.Equal(HttpStatusCode.OK, encResponse.StatusCode);

        var encResult = await encResponse.Content.ReadFromJsonAsync<EncryptPinResult>();
        Assert.NotNull(encResult);
        Assert.False(string.IsNullOrEmpty(encResult.EncryptedPinBlock));

        var decResponse = await _client.PostAsJsonAsync("/api/hsm/pin/decrypt",
            new { EncryptedPinBlock = encResult.EncryptedPinBlock, Pan = "4111111111111111", ZpkId = "default-zpk" });
        Assert.Equal(HttpStatusCode.OK, decResponse.StatusCode);

        var decResult = await decResponse.Content.ReadFromJsonAsync<DecryptPinResult>();
        Assert.NotNull(decResult);
        Assert.Equal("1234", decResult.Pin);
    }

    [Fact]
    public async Task EncryptPin_UnknownZpk_ReturnsNotFound()
    {
        // Trying to encrypt with a nonexistent ZPK should return 404.
        var response = await _client.PostAsJsonAsync("/api/hsm/pin/encrypt",
            new { Pin = "1234", Pan = "4111111111111111", ZpkId = "no-such-key" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task VerifyPin_CorrectPin_ReturnsTrue()
    {
        // Encrypt PIN "5678", then verify against "5678" - should match.
        var encResponse = await _client.PostAsJsonAsync("/api/hsm/pin/encrypt",
            new { Pin = "5678", Pan = "4222222222222222", ZpkId = "default-zpk" });
        var encResult = await encResponse.Content.ReadFromJsonAsync<EncryptPinResult>();

        var verifyResponse = await _client.PostAsJsonAsync("/api/hsm/pin/verify",
            new { EncryptedPinBlock = encResult!.EncryptedPinBlock, Pan = "4222222222222222", ZpkId = "default-zpk", ExpectedPin = "5678" });
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var verifyResult = await verifyResponse.Content.ReadFromJsonAsync<VerifyPinResult>();
        Assert.NotNull(verifyResult);
        Assert.True(verifyResult.Verified);
    }

    [Fact]
    public async Task VerifyPin_WrongPin_ReturnsFalse()
    {
        // Encrypt PIN "1234", then verify against "9999" - should NOT match.
        var encResponse = await _client.PostAsJsonAsync("/api/hsm/pin/encrypt",
            new { Pin = "1234", Pan = "4111111111111111", ZpkId = "default-zpk" });
        var encResult = await encResponse.Content.ReadFromJsonAsync<EncryptPinResult>();

        var verifyResponse = await _client.PostAsJsonAsync("/api/hsm/pin/verify",
            new { EncryptedPinBlock = encResult!.EncryptedPinBlock, Pan = "4111111111111111", ZpkId = "default-zpk", ExpectedPin = "9999" });
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var verifyResult = await verifyResponse.Content.ReadFromJsonAsync<VerifyPinResult>();
        Assert.NotNull(verifyResult);
        Assert.False(verifyResult.Verified);
    }

    [Fact]
    public async Task TranslatePin_RoundTrip()
    {
        // Generate two ZPKs, encrypt under one, translate to the other, decrypt under the second.
        await _client.PostAsJsonAsync("/api/hsm/keys/generate",
            new { KeyType = 1, KeyId = "translate-src" });  // 1 = ZPK, required for PIN operations
        await _client.PostAsJsonAsync("/api/hsm/keys/generate",
            new { KeyType = 1, KeyId = "translate-dst" });

        var encResponse = await _client.PostAsJsonAsync("/api/hsm/pin/encrypt",
            new { Pin = "9988", Pan = "4111111111111111", ZpkId = "translate-src" });
        var encResult = await encResponse.Content.ReadFromJsonAsync<EncryptPinResult>();

        // Translate from source ZPK to destination ZPK
        var translateResponse = await _client.PostAsJsonAsync("/api/hsm/pin/translate",
            new { EncryptedPinBlock = encResult!.EncryptedPinBlock, SourceZpkId = "translate-src", DestZpkId = "translate-dst", Pan = "4111111111111111" });
        Assert.Equal(HttpStatusCode.OK, translateResponse.StatusCode);

        var translateResult = await translateResponse.Content.ReadFromJsonAsync<TranslatePinResult>();

        // Decrypt under the destination key should yield the original PIN
        var decResponse = await _client.PostAsJsonAsync("/api/hsm/pin/decrypt",
            new { EncryptedPinBlock = translateResult!.EncryptedPinBlock, Pan = "4111111111111111", ZpkId = "translate-dst" });
        var decResult = await decResponse.Content.ReadFromJsonAsync<DecryptPinResult>();
        Assert.Equal("9988", decResult!.Pin);
    }

    // ---------------------------------------------------------------
    // ISO 8583 Tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task Iso8583_ParseBuild_RoundTrip()
    {
        // Build an ISO 8583 message, then parse it back.
        var buildResponse = await _client.PostAsJsonAsync("/api/iso8583/build",
            new { Mti = "0100", Fields = new Dictionary<string, string> { ["2"] = "4111111111111111", ["4"] = "000000001000", ["49"] = "USD" } });
        Assert.Equal(HttpStatusCode.OK, buildResponse.StatusCode);

        var buildResult = await buildResponse.Content.ReadFromJsonAsync<BuildIsoResult>();
        Assert.NotNull(buildResult);
        Assert.False(string.IsNullOrEmpty(buildResult.IsoMessage));

        var parseResponse = await _client.PostAsJsonAsync("/api/iso8583/parse",
            new { IsoMessage = buildResult.IsoMessage });
        Assert.Equal(HttpStatusCode.OK, parseResponse.StatusCode);

        var parseResult = await parseResponse.Content.ReadFromJsonAsync<ParseIsoResult>();
        Assert.NotNull(parseResult);
        Assert.Equal("0100", parseResult.Mti);
    }

    [Fact]
    public async Task Iso8583_ParseEmptyMessage_ReturnsBadRequest()
    {
        // Empty ISO message should return 400.
        var response = await _client.PostAsJsonAsync("/api/iso8583/parse",
            new { IsoMessage = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Iso8583_BuildEmptyFields_ReturnsBadRequest()
    {
        // No fields provided should return 400.
        var response = await _client.PostAsJsonAsync("/api/iso8583/build",
            new { Mti = "0100", Fields = new Dictionary<string, string>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Iso8583_GetFieldDefinitions_ReturnsList()
    {
        // GET /api/iso8583/fields should return the list of supported field definitions.
        var response = await _client.GetAsync("/api/iso8583/fields");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Iso8583_AuthorizeCard_ValidPan_Approved()
    {
        // Card authorization with a valid PAN should be approved.
        var response = await _client.PostAsJsonAsync("/api/iso8583/authorize",
            new
            {
                Pan = "4111111111111111",
                Amount = 100.00,
                Currency = "USD",
                EncryptedPinBlock = "AABBCCDDEEFF0011",
                ZpkId = "default-zpk",
                TerminalId = "TERM001",
                MerchantId = "MERCHANT001"
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AuthorizeResult>();
        Assert.NotNull(result);
        Assert.True(result.Approved);
    }

    [Fact]
    public async Task Iso8583_AuthorizeCard_DenyListPan_Declined()
    {
        // PANs starting with "4999" are on the deny list and should be declined.
        var response = await _client.PostAsJsonAsync("/api/iso8583/authorize",
            new
            {
                Pan = "4999111111111111",
                Amount = 100.00,
                Currency = "USD",
                EncryptedPinBlock = "AABBCCDDEEFF0011",
                ZpkId = "default-zpk",
                TerminalId = "TERM001",
                MerchantId = "MERCHANT001"
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AuthorizeResult>();
        Assert.NotNull(result);
        Assert.False(result.Approved);
    }

    [Fact]
    public async Task Iso8583_AuthorizeCard_UnknownZpk_ReturnsNotFound()
    {
        // Authorizing with a nonexistent ZPK should return 404.
        var response = await _client.PostAsJsonAsync("/api/iso8583/authorize",
            new
            {
                Pan = "4111111111111111",
                Amount = 100.00,
                Currency = "USD",
                EncryptedPinBlock = "AABBCCDDEEFF0011",
                ZpkId = "no-such-key",
                TerminalId = "TERM001",
                MerchantId = "MERCHANT001"
            });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Dispose() is called by xUnit when the test class is done.
    // Disposes the factory (shuts down the test server) and the HTTP client.
    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---------------------------------------------------------------
    // Response DTO records - used to deserialize JSON responses.
    // These mirror the JSON structure returned by the API endpoints.
    // Using "record" gives us value-based equality and concise syntax.
    // The property names must match the JSON keys (case-insensitive by default).
    // ---------------------------------------------------------------
    private record HsmHealthResult(string Service, string Status, int KeyCount);
    private record KeyListResult(List<string> KeyIds);
    private record GenerateKeyResult(string KeyId, int KeyType, string KeyCheckValue, string EncryptedUnderLmk);
    private record EncryptPinResult(string EncryptedPinBlock, string KeyCheckValue, string Format);
    private record DecryptPinResult(string Pin, string Format);
    private record VerifyPinResult(bool Verified, string Message);
    private record TranslatePinResult(string EncryptedPinBlock, string Format);
    private record BuildIsoResult(string IsoMessage, int Length);
    private record ParseIsoResult(string Mti, Dictionary<string, string> Fields, string MtiDescription);
    private record AuthorizeResult(bool Approved, string ResponseCode, string AuthorizationId, string Message);
}
