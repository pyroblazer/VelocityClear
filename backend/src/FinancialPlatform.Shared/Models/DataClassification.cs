using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.Models;

public class DataClassification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EntityName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public DataClassificationLevel Level { get; set; }
    public string MaskingRule { get; set; } = "Full";
    public bool RetentionRequired { get; set; }
    public int RetentionYears { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
