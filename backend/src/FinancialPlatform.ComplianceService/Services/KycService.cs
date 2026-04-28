using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;
using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Services;

public class KycService
{
    private readonly ComplianceDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<KycService> _logger;
    private static readonly Random _rng = new();

    public KycService(ComplianceDbContext db, IEventBus eventBus, ILogger<KycService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<KycProfileResponse> InitiateKycAsync(InitiateKycRequest request)
    {
        var existing = await _db.KycProfiles
            .FirstOrDefaultAsync(k => k.UserId == request.UserId && k.Status != KycStatus.Rejected && k.Status != KycStatus.Expired);
        if (existing != null)
            return MapToResponse(existing);

        var profile = new KycProfile
        {
            UserId = request.UserId,
            FullName = request.FullName,
            IdNumber = request.IdNumber,
            IdType = request.IdType,
            IdExpiryDate = request.IdExpiryDate,
            Status = KycStatus.InProgress
        };
        _db.KycProfiles.Add(profile);
        await _db.SaveChangesAsync();

        await _eventBus.PublishAsync(new KycStatusChangedEvent(
            profile.Id, profile.UserId, KycStatus.Pending, KycStatus.InProgress, null, DateTime.UtcNow));

        _logger.LogInformation("KYC initiated for user {UserId}, profile {ProfileId}", request.UserId, profile.Id);
        return MapToResponse(profile);
    }

    public async Task<LivenessCheckResponse> PerformLivenessCheckAsync(LivenessCheckRequest request)
    {
        var profile = await _db.KycProfiles.FindAsync(request.KycProfileId)
            ?? throw new KeyNotFoundException($"KYC profile {request.KycProfileId} not found");

        var confidence = 0.85 + _rng.NextDouble() * 0.14;
        var passed = confidence >= 0.90;

        profile.LivenessChecked = true;
        profile.LivenessConfidence = confidence;
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Liveness check for {ProfileId}: passed={Passed}, confidence={Confidence:F3}",
            request.KycProfileId, passed, confidence);

        return new LivenessCheckResponse(request.KycProfileId, passed, confidence,
            passed ? "Liveness verified successfully" : "Liveness check failed - confidence too low");
    }

    public async Task<WatchlistScreenResponse> ScreenWatchlistAsync(WatchlistScreenRequest request)
    {
        var profile = await _db.KycProfiles.FindAsync(request.KycProfileId)
            ?? throw new KeyNotFoundException($"KYC profile {request.KycProfileId} not found");

        var entries = await _db.WatchlistEntries
            .Where(w => w.IsActive)
            .ToListAsync();

        WatchlistEntry? hit = null;
        double bestScore = 0;

        foreach (var entry in entries)
        {
            var score = FuzzyMatch(request.FullName, entry.FullName);
            if (score > bestScore && score >= 0.80)
            {
                bestScore = score;
                hit = entry;
            }
        }

        profile.WatchlistScreened = true;
        profile.WatchlistHit = hit != null;
        profile.WatchlistMatchedCategory = hit?.Category.ToString();
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (hit != null)
        {
            await _eventBus.PublishAsync(new WatchlistHitDetectedEvent(
                profile.Id, profile.UserId, hit.FullName, hit.Category, bestScore, DateTime.UtcNow));
        }

        return new WatchlistScreenResponse(
            request.KycProfileId,
            hit != null,
            hit?.FullName,
            hit?.Category.ToString(),
            bestScore,
            hit != null ? $"Watchlist hit: {hit.Category}" : "No watchlist hits found");
    }

    public async Task<KycProfileResponse?> GetProfileAsync(string kycProfileId)
    {
        var profile = await _db.KycProfiles.FindAsync(kycProfileId);
        return profile == null ? null : MapToResponse(profile);
    }

    public async Task<KycProfileResponse?> GetProfileByUserAsync(string userId)
    {
        var profile = await _db.KycProfiles
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefaultAsync();
        return profile == null ? null : MapToResponse(profile);
    }

    public async Task<bool> IsUserVerifiedAsync(string userId)
    {
        return await _db.KycProfiles
            .AnyAsync(k => k.UserId == userId && k.Status == KycStatus.Verified);
    }

    public async Task<KycProfileResponse> UpdateStatusAsync(string kycProfileId, UpdateKycStatusRequest request)
    {
        var profile = await _db.KycProfiles.FindAsync(kycProfileId)
            ?? throw new KeyNotFoundException($"KYC profile {kycProfileId} not found");

        var old = profile.Status;
        profile.Status = request.NewStatus;
        profile.RejectionReason = request.Reason;
        profile.UpdatedAt = DateTime.UtcNow;

        if (request.NewStatus == KycStatus.Verified)
        {
            profile.VerifiedAt = DateTime.UtcNow;
            profile.ExpiresAt = DateTime.UtcNow.AddYears(2);
        }

        await _db.SaveChangesAsync();

        await _eventBus.PublishAsync(new KycStatusChangedEvent(
            profile.Id, profile.UserId, old, request.NewStatus, request.Reason, DateTime.UtcNow));

        return MapToResponse(profile);
    }

    private static double FuzzyMatch(string a, string b)
    {
        a = a.ToLowerInvariant().Trim();
        b = b.ToLowerInvariant().Trim();
        if (a == b) return 1.0;
        var longer = Math.Max(a.Length, b.Length);
        if (longer == 0) return 1.0;
        var distance = LevenshteinDistance(a, b);
        return (longer - distance) / (double)longer;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;
        for (int i = 1; i <= s.Length; i++)
            for (int j = 1; j <= t.Length; j++)
                d[i, j] = s[i - 1] == t[j - 1]
                    ? d[i - 1, j - 1]
                    : 1 + Math.Min(d[i - 1, j - 1], Math.Min(d[i - 1, j], d[i, j - 1]));
        return d[s.Length, t.Length];
    }

    private static KycProfileResponse MapToResponse(KycProfile p) => new(
        p.Id, p.UserId, p.Status, p.FullName, p.IdType,
        p.LivenessChecked, p.LivenessConfidence, p.WatchlistScreened, p.WatchlistHit,
        p.CreatedAt, p.UpdatedAt, p.VerifiedAt, p.ExpiresAt);
}
