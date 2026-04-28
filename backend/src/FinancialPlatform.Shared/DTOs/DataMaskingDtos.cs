using FinancialPlatform.Shared.Enums;

namespace FinancialPlatform.Shared.DTOs;

public record MaskDataRequest(
    string Value,
    string MaskingRule,
    DataClassificationLevel ClassificationLevel
);

public record MaskDataResponse(
    string OriginalLength,
    string MaskedValue,
    string MaskingRule,
    DataClassificationLevel ClassificationLevel
);

public record DataClassificationResponse(
    string Id,
    string EntityName,
    string FieldName,
    DataClassificationLevel Level,
    string MaskingRule,
    int RetentionYears
);
