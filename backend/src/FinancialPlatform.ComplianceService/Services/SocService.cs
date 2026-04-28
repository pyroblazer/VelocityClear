using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Services;

public class SocService
{
    private readonly ComplianceDbContext _db;
    private readonly ILogger<SocService> _logger;

    public SocService(ComplianceDbContext db, ILogger<SocService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IncidentResponse> CreateIncidentAsync(CreateIncidentRequest request)
    {
        var incident = new SecurityIncident
        {
            Title = request.Title,
            Description = request.Description,
            Severity = request.Severity,
            AffectedSystems = request.AffectedSystems,
            RunbookReference = request.RunbookReference
        };
        _db.SecurityIncidents.Add(incident);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Security incident created: {IncidentId} severity={Severity}",
            incident.Id, incident.Severity);
        return MapToResponse(incident);
    }

    public async Task<IncidentResponse?> UpdateIncidentAsync(string incidentId, UpdateIncidentRequest request)
    {
        var incident = await _db.SecurityIncidents.FindAsync(incidentId);
        if (incident == null) return null;

        incident.Status = request.NewStatus;
        if (request.ContainmentActions != null) incident.ContainmentActions = request.ContainmentActions;
        if (request.RootCause != null) incident.RootCause = request.RootCause;
        if (request.AssignedTo != null) incident.AssignedTo = request.AssignedTo;

        if (request.NewStatus == IncidentStatus.Contained) incident.ContainedAt = DateTime.UtcNow;
        if (request.NewStatus == IncidentStatus.Resolved) incident.ResolvedAt = DateTime.UtcNow;
        if (request.NewStatus == IncidentStatus.Closed) incident.ClosedAt = DateTime.UtcNow;

        incident.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapToResponse(incident);
    }

    public async Task<IEnumerable<IncidentResponse>> ListIncidentsAsync(IncidentStatus? status = null)
    {
        var query = _db.SecurityIncidents.AsQueryable();
        if (status.HasValue) query = query.Where(i => i.Status == status.Value);
        var incidents = await query.OrderByDescending(i => i.DetectedAt).ToListAsync();
        return incidents.Select(MapToResponse);
    }

    public async Task<SocDashboardResponse> GetDashboardAsync()
    {
        var since24h = DateTime.UtcNow.AddHours(-24);
        var all = await _db.SecurityIncidents.ToListAsync();

        return new SocDashboardResponse(
            all.Count(i => i.Status == IncidentStatus.Detected || i.Status == IncidentStatus.Investigating),
            all.Count(i => i.Severity == IncidentSeverity.Critical && i.Status < IncidentStatus.Resolved),
            all.Count(i => i.Severity == IncidentSeverity.High && i.Status < IncidentStatus.Resolved),
            all.Count(i => i.ResolvedAt >= since24h),
            all.OrderByDescending(i => i.DetectedAt).Take(5).Select(MapToResponse));
    }

    private static IncidentResponse MapToResponse(SecurityIncident i) => new(
        i.Id, i.Title, i.Description, i.Severity, i.Status, i.AssignedTo,
        i.RunbookReference, i.AffectedSystems, i.DetectedAt, i.ResolvedAt, i.CreatedAt);
}
