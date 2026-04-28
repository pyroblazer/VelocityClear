using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Events;
using FinancialPlatform.Shared.Interfaces;
using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Services;

public class AmlMonitoringService
{
    private readonly ComplianceDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<AmlMonitoringService> _logger;

    public AmlMonitoringService(ComplianceDbContext db, IEventBus eventBus, ILogger<AmlMonitoringService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<AmlAlert> CreateAlertAsync(string transactionId, string userId,
        string rule, AlertSeverity severity, decimal amount, string currency)
    {
        var alert = new AmlAlert
        {
            TransactionId = transactionId,
            UserId = userId,
            RuleTriggered = rule,
            Severity = severity,
            TransactionAmount = amount,
            Currency = currency
        };
        _db.AmlAlerts.Add(alert);
        await _db.SaveChangesAsync();

        await _eventBus.PublishAsync(new AmlAlertTriggeredEvent(
            alert.Id, transactionId, userId, rule, severity, amount, currency, DateTime.UtcNow));

        _logger.LogInformation("AML alert created: {AlertId} rule={Rule} severity={Severity}",
            alert.Id, rule, severity);
        return alert;
    }

    public async Task<AlertResponse?> ResolveAlertAsync(string alertId, ResolveAlertRequest request)
    {
        var alert = await _db.AmlAlerts.FindAsync(alertId);
        if (alert == null) return null;

        alert.Status = request.NewStatus;
        alert.ResolutionNotes = request.Resolution;
        alert.AssignedTo = request.ResolvedBy;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapToResponse(alert);
    }

    public async Task<IEnumerable<AlertResponse>> ListAlertsAsync(AlertStatus? status = null)
    {
        var query = _db.AmlAlerts.AsQueryable();
        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);
        var alerts = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return alerts.Select(MapToResponse);
    }

    public async Task<AlertResponse?> GetAlertAsync(string alertId)
    {
        var alert = await _db.AmlAlerts.FindAsync(alertId);
        return alert == null ? null : MapToResponse(alert);
    }

    private static AlertResponse MapToResponse(AmlAlert a) => new(
        a.Id, a.TransactionId, a.UserId, a.RuleTriggered,
        a.Severity, a.Status, a.AssignedTo,
        a.TransactionAmount, a.Currency, a.CreatedAt, a.UpdatedAt);
}
