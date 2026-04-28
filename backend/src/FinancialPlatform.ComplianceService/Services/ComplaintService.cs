using FinancialPlatform.ComplianceService.Data;
using FinancialPlatform.Shared.DTOs;
using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Services;

public class ComplaintService
{
    private readonly ComplianceDbContext _db;
    private readonly ILogger<ComplaintService> _logger;

    public ComplaintService(ComplianceDbContext db, ILogger<ComplaintService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ComplaintResponse> CreateComplaintAsync(CreateComplaintRequest request)
    {
        var ticket = new ComplaintTicket
        {
            UserId = request.UserId,
            Category = request.Category,
            Subject = request.Subject,
            Description = request.Description,
            RelatedTransactionId = request.RelatedTransactionId
        };
        _db.ComplaintTickets.Add(ticket);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Complaint created: {ComplaintId} category={Category}", ticket.Id, ticket.Category);
        return MapToResponse(ticket);
    }

    public async Task<ComplaintResponse?> AcknowledgeAsync(string complaintId)
    {
        var ticket = await _db.ComplaintTickets.FindAsync(complaintId);
        if (ticket == null) return null;

        ticket.Status = ComplaintStatus.Acknowledged;
        ticket.AcknowledgedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapToResponse(ticket);
    }

    public async Task<ComplaintResponse?> EscalateAsync(string complaintId, EscalateComplaintRequest request)
    {
        var ticket = await _db.ComplaintTickets.FindAsync(complaintId);
        if (ticket == null) return null;

        ticket.Status = ComplaintStatus.Escalated;
        ticket.EscalationLevel = request.NewLevel;
        ticket.UpdatedAt = DateTime.UtcNow;

        var note = new ComplaintNote
        {
            ComplaintId = complaintId,
            AuthorId = "system",
            Content = $"Escalated to {request.NewLevel}: {request.Reason}",
            IsInternal = true
        };
        _db.ComplaintNotes.Add(note);
        await _db.SaveChangesAsync();
        return MapToResponse(ticket);
    }

    public async Task<ComplaintResponse?> ResolveAsync(string complaintId, ResolveComplaintRequest request)
    {
        var ticket = await _db.ComplaintTickets.FindAsync(complaintId);
        if (ticket == null) return null;

        ticket.Status = ComplaintStatus.Resolved;
        ticket.Resolution = request.Resolution;
        ticket.AssignedTo = request.ResolvedBy;
        ticket.ResolvedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapToResponse(ticket);
    }

    public async Task<ComplaintResponse?> CloseAsync(string complaintId)
    {
        var ticket = await _db.ComplaintTickets.FindAsync(complaintId);
        if (ticket == null) return null;

        ticket.Status = ComplaintStatus.Closed;
        ticket.ClosedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapToResponse(ticket);
    }

    public async Task AddNoteAsync(string complaintId, AddComplaintNoteRequest request)
    {
        var note = new ComplaintNote
        {
            ComplaintId = complaintId,
            AuthorId = request.AuthorId,
            Content = request.Content,
            IsInternal = request.IsInternal
        };
        _db.ComplaintNotes.Add(note);
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<ComplaintResponse>> ListComplaintsAsync(string? userId = null)
    {
        var query = _db.ComplaintTickets.AsQueryable();
        if (userId != null)
            query = query.Where(c => c.UserId == userId);

        var tickets = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();

        // Check SLA breaches
        var now = DateTime.UtcNow;
        foreach (var t in tickets.Where(t => !t.SlaBreach && t.Status < ComplaintStatus.Resolved && t.SlaDeadline < now))
        {
            t.SlaBreach = true;
            t.UpdatedAt = now;
        }
        if (tickets.Any(t => t.SlaBreach))
            await _db.SaveChangesAsync();

        return tickets.Select(MapToResponse);
    }

    public async Task<ComplaintResponse?> GetComplaintAsync(string complaintId)
    {
        var ticket = await _db.ComplaintTickets.FindAsync(complaintId);
        return ticket == null ? null : MapToResponse(ticket);
    }

    private static ComplaintResponse MapToResponse(ComplaintTicket t) => new(
        t.Id, t.UserId, t.Category, t.Status, t.EscalationLevel,
        t.Subject, t.Description, t.AssignedTo, t.Resolution,
        t.CreatedAt, t.UpdatedAt, t.SlaDeadline, t.SlaBreach,
        t.RelatedTransactionId);
}
