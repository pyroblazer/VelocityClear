using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;
using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Services;

public class ConsentService
{
    private readonly ComplianceDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ConsentService> _logger;

    public ConsentService(ComplianceDbContext db, IEventBus eventBus, ILogger<ConsentService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<ConsentResponse> GrantConsentAsync(GrantConsentRequest request)
    {
        var existing = await _db.ConsentRecords
            .FirstOrDefaultAsync(c => c.UserId == request.UserId
                && c.ConsentType == request.ConsentType
                && c.Status == ConsentStatus.Granted);

        if (existing != null)
            return MapToResponse(existing);

        var record = new ConsentRecord
        {
            UserId = request.UserId,
            ConsentType = request.ConsentType,
            Status = ConsentStatus.Granted,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            LegalBasis = request.LegalBasis,
            ExpiresAt = DateTime.UtcNow.AddYears(1)
        };
        _db.ConsentRecords.Add(record);
        await _db.SaveChangesAsync();

        await _eventBus.PublishAsync(new ConsentGrantedEvent(
            record.Id, record.UserId, record.ConsentType, record.IpAddress, DateTime.UtcNow));

        _logger.LogInformation("Consent granted: user={UserId} type={Type}", request.UserId, request.ConsentType);
        return MapToResponse(record);
    }

    public async Task<ConsentResponse> WithdrawConsentAsync(WithdrawConsentRequest request)
    {
        var record = await _db.ConsentRecords
            .FirstOrDefaultAsync(c => c.UserId == request.UserId
                && c.ConsentType == request.ConsentType
                && c.Status == ConsentStatus.Granted)
            ?? throw new KeyNotFoundException($"No active consent found for user {request.UserId} type {request.ConsentType}");

        record.Status = ConsentStatus.Withdrawn;
        record.WithdrawnAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _eventBus.PublishAsync(new ConsentWithdrawnEvent(
            record.Id, record.UserId, record.ConsentType, request.Reason, DateTime.UtcNow));

        _logger.LogInformation("Consent withdrawn: user={UserId} type={Type}", request.UserId, request.ConsentType);
        return MapToResponse(record);
    }

    public async Task<IEnumerable<ConsentResponse>> ListConsentsByUserAsync(string userId)
    {
        var records = await _db.ConsentRecords
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.GrantedAt)
            .ToListAsync();
        return records.Select(MapToResponse);
    }

    public async Task<ConsentCheckResponse> CheckConsentAsync(string userId, ConsentType consentType)
    {
        var hasActive = await _db.ConsentRecords
            .AnyAsync(c => c.UserId == userId
                && c.ConsentType == consentType
                && c.Status == ConsentStatus.Granted
                && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow));
        return new ConsentCheckResponse(hasActive, consentType, userId);
    }

    private static ConsentResponse MapToResponse(ConsentRecord r) => new(
        r.Id, r.UserId, r.ConsentType, r.Status,
        r.GrantedAt, r.WithdrawnAt, r.ExpiresAt, r.LegalBasis);
}
