using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GalaReconciler.Web.Features.Reconciliation.Persistence;

public sealed class ReconciliationDbContext(DbContextOptions<ReconciliationDbContext> options) : DbContext(options)
{
    public DbSet<Run> Runs => Set<Run>();

    public DbSet<RunDiscrepancy> Discrepancies => Set<RunDiscrepancy>();

    public DbSet<RunWarning> Warnings => Set<RunWarning>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Run>(run =>
        {
            run.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            run.Property(r => r.SourceFileName).HasMaxLength(260);
            run.Property(r => r.ProcessedFileName).HasMaxLength(260);
            run.Property(r => r.ErrorTitle).HasMaxLength(500);
            run.HasIndex(r => r.CreatedAtUtc);
            run.HasIndex(r => r.Status);
            run.HasMany(r => r.Discrepancies).WithOne().HasForeignKey(d => d.RunId).OnDelete(DeleteBehavior.Cascade);
            run.HasMany(r => r.Warnings).WithOne().HasForeignKey(w => w.RunId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RunDiscrepancy>(discrepancy =>
        {
            discrepancy.Property(d => d.Type).HasConversion<string>().HasMaxLength(40);
            discrepancy.Property(d => d.MatchedBy).HasConversion<string>().HasMaxLength(20);
            discrepancy.HasIndex(d => new { d.RunId, d.Position });
            discrepancy.HasIndex(d => d.GuestDisplayName);
        });

        modelBuilder.Entity<RunWarning>().HasIndex(w => new { w.RunId, w.Position });
    }
}

/// <summary>Lets <c>dotnet ef migrations add</c> create the context without starting the web host.</summary>
public sealed class DesignTimeReconciliationDbContextFactory : IDesignTimeDbContextFactory<ReconciliationDbContext>
{
    public ReconciliationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ReconciliationDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
