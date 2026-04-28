// ============================================================================
// ComplianceDbContext.cs - Entity Framework Core Database Context for Compliance
// ============================================================================
// This file defines the database context for the Compliance Service. It maps
// the AuditLog entity to a database table and configures columns, constraints,
// and indexes using EF Core's Fluent API.
//
// Key concepts:
//   - DbContext: EF Core's base class for database interaction. Each instance
//     represents a session with the database.
//   - DbSet<T>: Represents a database table for entity type T. LINQ queries
//     against DbSet<T> are translated to SQL by EF Core.
//   - OnModelCreating: Override this method to configure entity-to-table
//     mappings using the Fluent API (an alternative to data annotation attributes).
//   - HasKey(), Property(), HasIndex(): Fluent API methods for configuring
//     primary keys, columns, and database indexes respectively.
// ============================================================================

using FinancialPlatform.Shared.Enums;
using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.ComplianceService.Data;

// Inherits from DbContext (EF Core's base class). The generic type parameter
// on DbContextOptions ensures type safety - you can't accidentally pass options
// for a different DbContext.
public class ComplianceDbContext : DbContext
{
    // Constructor accepts DbContextOptions and passes it to the base class.
    // The options contain the database provider (SQL Server) and connection string,
    // configured in Program.cs via AddDbContext<ComplianceDbContext>(...).
    public ComplianceDbContext(DbContextOptions<ComplianceDbContext> options) : base(options) { }

    // DbSet<AuditLog> represents the "AuditLogs" table. EF Core convention
    // pluralizes the property name to determine the table name.
    // "get; set;" defines a standard auto-property with getter and setter.
    // (Compare with TransactionDbContext which uses expression-bodied "=>" syntax.)
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<KycProfile> KycProfiles { get; set; }
    public DbSet<ConsentRecord> ConsentRecords { get; set; }
    public DbSet<DataClassification> DataClassifications { get; set; }
    public DbSet<WatchlistEntry> WatchlistEntries { get; set; }
    public DbSet<SuspiciousActivityReport> SuspiciousActivityReports { get; set; }
    public DbSet<AmlAlert> AmlAlerts { get; set; }
    public DbSet<ApprovalRequest> ApprovalRequests { get; set; }
    public DbSet<RoleAssignment> RoleAssignments { get; set; }
    public DbSet<OjkReport> OjkReports { get; set; }
    public DbSet<AuditRetentionPolicy> AuditRetentionPolicies { get; set; }
    public DbSet<ComplaintTicket> ComplaintTickets { get; set; }
    public DbSet<ComplaintNote> ComplaintNotes { get; set; }
    public DbSet<SignedDocument> SignedDocuments { get; set; }
    public DbSet<SecurityIncident> SecurityIncidents { get; set; }
    public DbSet<DrpBcpStatus> DrpBcpStatuses { get; set; }
    public DbSet<DataResidencyCheck> DataResidencyChecks { get; set; }
    public DbSet<VendorAuditEntry> VendorAuditEntries { get; set; }

    // OnModelCreating is called once when the DbContext is first used. It's the
    // place to configure how your C# entity classes map to database schema.
    // "protected override" means: this method overrides a virtual method from the
    // base class, and can only be called from this class or its subclasses.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure the AuditLog entity's database mapping.
        modelBuilder.Entity<AuditLog>(entity =>
        {
            // HasKey() defines the primary key column.
            entity.HasKey(e => e.Id);

            // Property() configures individual columns:
            //   - IsRequired() makes the column NOT NULL.
            //   - HasMaxLength(100) limits the column to 100 characters.
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Payload).IsRequired();

            // Hash and PreviousHash columns store the SHA-256 cryptographic hashes
            // used for tamper-proof chain verification.
            entity.Property(e => e.Hash).IsRequired().HasMaxLength(255);

            // HasIndex() creates a database index to speed up queries that filter
            // or sort by EventType or CreatedAt (common query patterns for audit logs).
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<KycProfile>(e =>
        {
            e.HasKey(k => k.Id);
            e.Property(k => k.UserId).IsRequired().HasMaxLength(100);
            e.HasIndex(k => k.UserId);
            e.HasIndex(k => k.Status);
        });

        modelBuilder.Entity<ConsentRecord>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.UserId).IsRequired().HasMaxLength(100);
            e.HasIndex(c => new { c.UserId, c.ConsentType });
        });

        modelBuilder.Entity<DataClassification>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.EntityName, d.FieldName }).IsUnique();
        });

        modelBuilder.Entity<WatchlistEntry>(e =>
        {
            e.HasKey(w => w.Id);
            e.HasIndex(w => w.Category);
            e.HasIndex(w => w.IsActive);
        });

        modelBuilder.Entity<SuspiciousActivityReport>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.Status);
            e.HasIndex(s => s.TransactionId);
        });

        modelBuilder.Entity<AmlAlert>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.Status);
            e.HasIndex(a => a.TransactionId);
        });

        modelBuilder.Entity<ApprovalRequest>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.Status);
            e.HasIndex(a => a.RequestedBy);
        });

        modelBuilder.Entity<RoleAssignment>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.UserId, r.IsActive });
        });

        modelBuilder.Entity<OjkReport>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.ReportType);
            e.HasIndex(r => r.Status);
        });

        modelBuilder.Entity<ComplaintTicket>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.UserId);
            e.HasIndex(c => c.Status);
        });

        modelBuilder.Entity<ComplaintNote>(e =>
        {
            e.HasKey(n => n.Id);
            e.HasIndex(n => n.ComplaintId);
        });

        modelBuilder.Entity<SignedDocument>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.DocumentId);
        });

        modelBuilder.Entity<SecurityIncident>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => i.Status);
            e.HasIndex(i => i.Severity);
        });

        modelBuilder.Entity<DrpBcpStatus>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.PlanName).IsUnique();
        });

        // Seed watchlist entries
        modelBuilder.Entity<WatchlistEntry>().HasData(
            new WatchlistEntry { Id = "wl-001", FullName = "Test Sanctioned Person", Category = WatchlistCategory.Sanction, Source = "OFAC-Seed", IsActive = true, AddedAt = new DateTime(2024, 1, 1) },
            new WatchlistEntry { Id = "wl-002", FullName = "Test PEP Individual", Category = WatchlistCategory.PEP, Source = "Local-Seed", IsActive = true, AddedAt = new DateTime(2024, 1, 1) }
        );

        // Seed data classifications
        modelBuilder.Entity<DataClassification>().HasData(
            new DataClassification { Id = "dc-001", EntityName = "User", FieldName = "IdNumber", Level = DataClassificationLevel.SensitivePII, MaskingRule = "Partial", RetentionRequired = true, RetentionYears = 10, CreatedAt = new DateTime(2024, 1, 1) },
            new DataClassification { Id = "dc-002", EntityName = "User", FieldName = "FullName", Level = DataClassificationLevel.PII, MaskingRule = "Partial", RetentionRequired = true, RetentionYears = 10, CreatedAt = new DateTime(2024, 1, 1) },
            new DataClassification { Id = "dc-003", EntityName = "Transaction", FieldName = "Amount", Level = DataClassificationLevel.Confidential, MaskingRule = "Full", RetentionRequired = true, RetentionYears = 7, CreatedAt = new DateTime(2024, 1, 1) },
            new DataClassification { Id = "dc-004", EntityName = "User", FieldName = "Email", Level = DataClassificationLevel.PII, MaskingRule = "Email", RetentionRequired = true, RetentionYears = 10, CreatedAt = new DateTime(2024, 1, 1) },
            new DataClassification { Id = "dc-005", EntityName = "User", FieldName = "PhoneNumber", Level = DataClassificationLevel.PII, MaskingRule = "Phone", RetentionRequired = true, RetentionYears = 10, CreatedAt = new DateTime(2024, 1, 1) }
        );

        // Seed audit retention policies
        modelBuilder.Entity<AuditRetentionPolicy>().HasData(
            new AuditRetentionPolicy { Id = "rp-001", EventType = "TransactionCreatedEvent", RetentionYears = 10, Regulation = "POJK No.6/POJK.07/2022", IsActive = true, CreatedAt = new DateTime(2024, 1, 1) },
            new AuditRetentionPolicy { Id = "rp-002", EventType = "PaymentAuthorizedEvent", RetentionYears = 10, Regulation = "POJK No.6/POJK.07/2022", IsActive = true, CreatedAt = new DateTime(2024, 1, 1) },
            new AuditRetentionPolicy { Id = "rp-003", EventType = "KycStatusChangedEvent", RetentionYears = 5, Regulation = "POJK No.12/POJK.01/2017", IsActive = true, CreatedAt = new DateTime(2024, 1, 1) }
        );
    }
}
