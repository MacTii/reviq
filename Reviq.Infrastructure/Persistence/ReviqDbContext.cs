using Microsoft.EntityFrameworkCore;
using Reviq.Infrastructure.Persistence.Entities;

namespace Reviq.Infrastructure.Persistence;

public sealed class ReviqDbContext(DbContextOptions<ReviqDbContext> options) : DbContext(options)
{
    public DbSet<ReviewResultRecord> Reviews => Set<ReviewResultRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReviewResultRecord>(e =>
        {
            e.HasKey(r => r.ReviewId);
            e.HasIndex(r => r.CreatedAt);
            e.HasMany(r => r.Files)
                .WithOne()
                .HasForeignKey(f => f.ReviewResultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FileReviewRecord>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasMany(f => f.Issues)
                .WithOne()
                .HasForeignKey(i => i.FileReviewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReviewIssueRecord>(e => e.HasKey(i => i.Id));
    }
}
