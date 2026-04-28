using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Services;

public class InfrastructureComplianceService
{
    private readonly ComplianceDbContext _db;
    private readonly ILogger<InfrastructureComplianceService> _logger;

    public InfrastructureComplianceService(ComplianceDbContext db, ILogger<InfrastructureComplianceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IEnumerable<DrpStatusResponse>> GetDrpStatusAsync()
    {
        var plans = await _db.DrpBcpStatuses.ToListAsync();
        return plans.Select(p => new DrpStatusResponse(
            p.Id, p.PlanName, p.Status, p.RtoMinutes, p.RpoMinutes,
            p.LastTestedAt, p.NextTestScheduled, p.LastTestPassed));
    }

    public async Task<DrpStatusResponse> UpsertDrpPlanAsync(string planName, int rtoMinutes, int rpoMinutes)
    {
        var plan = await _db.DrpBcpStatuses.FirstOrDefaultAsync(p => p.PlanName == planName);
        if (plan == null)
        {
            plan = new DrpBcpStatus { PlanName = planName, RtoMinutes = rtoMinutes, RpoMinutes = rpoMinutes };
            _db.DrpBcpStatuses.Add(plan);
        }
        else
        {
            plan.RtoMinutes = rtoMinutes;
            plan.RpoMinutes = rpoMinutes;
            plan.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return new DrpStatusResponse(plan.Id, plan.PlanName, plan.Status,
            plan.RtoMinutes, plan.RpoMinutes, plan.LastTestedAt, plan.NextTestScheduled, plan.LastTestPassed);
    }

    public async Task<IEnumerable<DataResidencyResponse>> CheckDataResidencyAsync()
    {
        var checks = await _db.DataResidencyChecks.ToListAsync();
        return checks.Select(c => new DataResidencyResponse(
            c.ServiceName, c.Region, c.IsCompliant, c.NonComplianceReason, c.CheckedAt));
    }

    public async Task<DataResidencyResponse> RecordResidencyCheckAsync(string service, string region, bool compliant, string? reason = null)
    {
        var check = new DataResidencyCheck
        {
            ServiceName = service,
            DataCategory = "All",
            Region = region,
            IsCompliant = compliant,
            NonComplianceReason = reason
        };
        _db.DataResidencyChecks.Add(check);
        await _db.SaveChangesAsync();
        return new DataResidencyResponse(service, region, compliant, reason, check.CheckedAt);
    }

    public async Task<IEnumerable<VendorAuditResponse>> GetVendorAuditsAsync()
    {
        var entries = await _db.VendorAuditEntries.OrderByDescending(v => v.PeriodEnd).ToListAsync();
        return entries.Select(v => new VendorAuditResponse(
            v.VendorName, v.ServiceType, v.UptimePercent, v.SlaTargetPercent,
            v.SlaMet, v.IncidentCount, v.PeriodStart, v.PeriodEnd));
    }

    public async Task<VendorAuditResponse> RecordVendorAuditAsync(string vendor, string serviceType,
        double uptime, double slaTarget, int incidents, DateTime periodStart, DateTime periodEnd)
    {
        var entry = new VendorAuditEntry
        {
            VendorName = vendor,
            ServiceType = serviceType,
            UptimePercent = uptime,
            SlaTargetPercent = slaTarget,
            SlaMet = uptime >= slaTarget,
            IncidentCount = incidents,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd
        };
        _db.VendorAuditEntries.Add(entry);
        await _db.SaveChangesAsync();
        return new VendorAuditResponse(vendor, serviceType, uptime, slaTarget,
            entry.SlaMet, incidents, periodStart, periodEnd);
    }
}
