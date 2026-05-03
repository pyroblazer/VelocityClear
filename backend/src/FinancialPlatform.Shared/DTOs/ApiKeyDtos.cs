namespace FinancialPlatform.Shared.DTOs;

public class CreateApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public string[] Permissions { get; set; } = [];
    public DateTime? ExpiresAt { get; set; }
}

public class ApiKeyResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string[] Permissions { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; }
}

public class CreateApiKeyResponse : ApiKeyResponse
{
    public string ApiKey { get; set; } = string.Empty; // Full key shown only once
}
