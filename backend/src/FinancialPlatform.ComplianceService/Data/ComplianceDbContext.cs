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
    }
}
