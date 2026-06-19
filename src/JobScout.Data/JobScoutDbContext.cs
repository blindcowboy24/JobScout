using JobScout.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobScout.Data;

/// <summary>
/// The EF Core context. Kept provider-agnostic on purpose — the concrete provider (SQLite for
/// zero-setup local dev, SQL Server when configured) is chosen at registration time, not here,
/// so nothing in this class assumes either one.
/// </summary>
public class JobScoutDbContext(DbContextOptions<JobScoutDbContext> options) : DbContext(options)
{
    public DbSet<TrackedPosting> TrackedPostings => Set<TrackedPosting>();
    public DbSet<PostingSnapshot> Snapshots => Set<PostingSnapshot>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<TrackedPosting>(e =>
        {
            e.HasKey(p => p.Id);

            // Natural identity of a posting within the feed it was found through.
            e.HasIndex(p => new { p.Source, p.Feed, p.ExternalId }).IsUnique();

            // Hot path for the ranked read-out and for "active per board" reconciliation.
            e.HasIndex(p => new { p.IsActive, p.Score });

            e.Property(p => p.Source).HasConversion<string>().HasMaxLength(32);
            e.Property(p => p.Band).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.Remote).HasConversion<string>().HasMaxLength(16);

            e.Property(p => p.Feed).HasMaxLength(128);
            e.Property(p => p.Company).HasMaxLength(128);
            e.Property(p => p.ExternalId).HasMaxLength(128);
            e.Property(p => p.Title).HasMaxLength(512);
            e.Property(p => p.Description).HasMaxLength(4000);
            e.Property(p => p.SalaryCurrency).HasMaxLength(8);
            e.Property(p => p.SalaryInterval).HasMaxLength(16);

            // Explicit precision keeps SQL Server happy; SQLite ignores it harmlessly.
            e.Property(p => p.SalaryMin).HasPrecision(18, 2);
            e.Property(p => p.SalaryMax).HasPrecision(18, 2);

            e.HasMany(p => p.Snapshots)
                .WithOne(s => s.TrackedPosting!)
                .HasForeignKey(s => s.TrackedPostingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostingSnapshot>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.TrackedPostingId, s.ObservedAt });
            e.Property(s => s.ContentHash).HasMaxLength(64);
        });
    }
}
