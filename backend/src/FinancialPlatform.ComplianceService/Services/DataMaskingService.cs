using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Services;

public class DataMaskingService
{
    private readonly ComplianceDbContext _db;

    public DataMaskingService(ComplianceDbContext db)
    {
        _db = db;
    }

    public MaskDataResponse MaskValue(MaskDataRequest request)
    {
        var masked = request.MaskingRule switch
        {
            "Full" => new string('*', request.Value.Length),
            "Partial" => MaskPartial(request.Value),
            "LastFour" => MaskLastFour(request.Value),
            "Email" => MaskEmail(request.Value),
            "Phone" => MaskPhone(request.Value),
            _ => new string('*', request.Value.Length)
        };

        return new MaskDataResponse(
            request.Value.Length.ToString(),
            masked,
            request.MaskingRule,
            request.ClassificationLevel);
    }

    public async Task<IEnumerable<DataClassificationResponse>> ListClassificationsAsync()
    {
        var items = await _db.DataClassifications.ToListAsync();
        return items.Select(d => new DataClassificationResponse(
            d.Id, d.EntityName, d.FieldName, d.Level, d.MaskingRule, d.RetentionYears));
    }

    private static string MaskPartial(string value)
    {
        if (value.Length <= 4) return new string('*', value.Length);
        var visible = Math.Max(2, value.Length / 4);
        return value[..visible] + new string('*', value.Length - visible * 2) + value[^visible..];
    }

    private static string MaskLastFour(string value)
    {
        if (value.Length <= 4) return value;
        return new string('*', value.Length - 4) + value[^4..];
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return new string('*', email.Length);
        var local = email[..at];
        var domain = email[at..];
        var visibleLocal = local.Length > 2 ? local[..2] : local[..1];
        return visibleLocal + new string('*', local.Length - visibleLocal.Length) + domain;
    }

    private static string MaskPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return phone;
        return new string('*', digits.Length - 4) + digits[^4..];
    }
}
