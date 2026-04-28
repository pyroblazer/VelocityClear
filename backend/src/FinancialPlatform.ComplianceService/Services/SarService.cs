using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;
using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Services;

public class SarService
{
    private readonly ComplianceDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<SarService> _logger;

    public SarService(ComplianceDbContext db, IEventBus eventBus, ILogger<SarService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<SarResponse> FileSarAsync(SarFilingRequest request)
    {
        var sar = new SuspiciousActivityReport
        {
            TransactionId = request.TransactionId,
            UserId = request.UserId,
            Narrative = request.Narrative,
            SuspiciousAmount = request.SuspiciousAmount,
            SuspiciousBasis = request.SuspiciousBasis,
            FiledBy = request.FiledBy,
            FiledAt = DateTime.UtcNow,
            Status = SarStatus.Filed,
            OjkReferenceNumber = $"SAR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}"
        };
        _db.SuspiciousActivityReports.Add(sar);
        await _db.SaveChangesAsync();

        await _eventBus.PublishAsync(new SarFiledEvent(
            sar.Id, sar.TransactionId, sar.UserId, sar.FiledBy!,
            sar.SuspiciousAmount, DateTime.UtcNow));

        _logger.LogInformation("SAR filed: {SarId} ref={Ref}", sar.Id, sar.OjkReferenceNumber);
        return MapToResponse(sar);
    }

    public async Task<IEnumerable<SarResponse>> ListSarsAsync(SarStatus? status = null)
    {
        var query = _db.SuspiciousActivityReports.AsQueryable();
        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);
        var sars = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
        return sars.Select(MapToResponse);
    }

    public async Task<SarResponse?> GetSarAsync(string sarId)
    {
        var sar = await _db.SuspiciousActivityReports.FindAsync(sarId);
        return sar == null ? null : MapToResponse(sar);
    }

    public async Task<SarResponse?> UpdateSarStatusAsync(string sarId, SarStatus newStatus, string? notes)
    {
        var sar = await _db.SuspiciousActivityReports.FindAsync(sarId);
        if (sar == null) return null;

        sar.Status = newStatus;
        sar.ReviewNotes = notes;
        sar.UpdatedAt = DateTime.UtcNow;
        if (newStatus == SarStatus.Acknowledged)
            sar.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToResponse(sar);
    }

    private static SarResponse MapToResponse(SuspiciousActivityReport s) => new(
        s.Id, s.TransactionId, s.UserId, s.Narrative,
        s.Status, s.OjkReferenceNumber, s.FiledBy, s.FiledAt,
        s.SuspiciousAmount, s.CreatedAt);
}
