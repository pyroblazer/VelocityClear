using System.Security.Cryptography;
using System.Text;

namespace FinancialPlatform.ComplianceService.Services;

public class WormStorageService
{
    private readonly Dictionary<string, (string Content, string Hash, DateTime StoredAt)> _store = new();
    private readonly ILogger<WormStorageService> _logger;

    public WormStorageService(ILogger<WormStorageService> logger)
    {
        _logger = logger;
    }

    public void Store(string key, string content)
    {
        if (_store.ContainsKey(key))
            throw new InvalidOperationException($"WORM violation: key '{key}' already exists and cannot be overwritten");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        _store[key] = (content, hash, DateTime.UtcNow);
        _logger.LogInformation("WORM stored: key={Key} hash={Hash}", key, hash[..16]);
    }

    public bool Verify(string key, string content)
    {
        if (!_store.TryGetValue(key, out var stored))
            return false;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        return hash == stored.Hash;
    }

    public bool Exists(string key) => _store.ContainsKey(key);

    public int Count => _store.Count;
}
