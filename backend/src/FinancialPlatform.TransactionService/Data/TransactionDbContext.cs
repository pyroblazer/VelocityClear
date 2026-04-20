// ============================================================================
// TransactionDbContext.cs - Entity Framework Core Database Context
// ============================================================================
// This file defines the database context for the Transaction Service. A DbContext
// is Entity Framework Core's main entry point for database operations - it
// represents a session with the database and provides APIs for querying and
// saving entity objects.
//
// Key concepts:
//   - DbContext: The base class for EF Core database contexts. Each DbContext
//     typically maps to a database, and each DbSet<T> maps to a table.
//   - DbSet<T>: A strongly-typed collection that represents a database table.
//     LINQ queries against DbSet<T> are translated to SQL automatically.
//   - OnModelCreating: A method you override to configure how entity classes
//     map to database tables (column types, indexes, constraints, etc.).
// ============================================================================

using FinancialPlatform.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialPlatform.TransactionService.Data;

// TransactionDbContext inherits from DbContext, which is EF Core's base class.
// The constructor accepts DbContextOptions<TransactionDbContext> - this is how
// the database provider (SQL Server, InMemory, etc.) and connection string are
// configured at runtime via dependency injection.
public class TransactionDbContext : DbContext
{
    // The ": base(options)" syntax calls the DbContext base class constructor,
    // passing along the configured options (database provider, connection string).
    public TransactionDbContext(DbContextOptions<TransactionDbContext> options) : base(options)
    {
    }

    // DbSet<Transaction> represents the "Transactions" table in the database.
    // The "=>" (expression-bodied property) is shorthand for a getter that returns
    // Set<Transaction>(). Set<T>() is a DbContext method that creates/returns
    // the DbSet for the given entity type.
    public DbSet<Transaction> Transactions => Set<Transaction>();

    // DbSet<User> represents the "Users" table.
    public DbSet<User> Users => Set<User>();

    // OnModelCreating is called by EF Core once when the context is first used.
    // It's where you use the "Fluent API" to configure entity-to-table mappings.
    // This is an alternative to using data annotations (attributes like [Required])
    // on your model classes, and offers more control.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Always call the base implementation first - it applies any default conventions.
        base.OnModelCreating(modelBuilder);

        // modelBuilder.Entity<T>() returns an EntityTypeConfiguration object that
        // lets you fluently chain configuration calls for that entity type.
        modelBuilder.Entity<Transaction>(entity =>
        {
            // HasKey() defines the primary key for this entity/table.
            entity.HasKey(t => t.Id);

            // Property() configures a specific column. IsRequired() means the column
            // is NOT NULL in the database.
            entity.Property(t => t.UserId).IsRequired();

            // HasColumnType("decimal(18,2)") specifies the SQL column type -
            // 18 digits total, 2 after the decimal point (e.g., 9999999999999999.99).
            entity.Property(t => t.Amount).HasColumnType("decimal(18,2)");

            // HasMaxLength(3) restricts the column to 3 characters (e.g., "USD", "EUR").
            entity.Property(t => t.Currency).HasMaxLength(3).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(500);
            entity.Property(t => t.Counterparty).HasMaxLength(200);

            // HasIndex() creates a database index on the specified column, which
            // speeds up queries that filter or sort by that column.
            entity.HasIndex(t => t.UserId);
            entity.HasIndex(t => t.Timestamp);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Username).HasMaxLength(100).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();

            // IsUnique() creates a unique index, enforcing that no two users
            // can have the same username - the database will reject duplicates.
            entity.HasIndex(u => u.Username).IsUnique();
        });
    }
}
