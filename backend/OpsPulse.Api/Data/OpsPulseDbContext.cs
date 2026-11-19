using Microsoft.EntityFrameworkCore;
using OpsPulse.Api.Domain;

namespace OpsPulse.Api.Data;

public sealed class OpsPulseDbContext(DbContextOptions<OpsPulseDbContext> options) : DbContext(options)
{
    public DbSet<MonitoredService> Services => Set<MonitoredService>();
    public DbSet<HealthCheckRecord> HealthChecks => Set<HealthCheckRecord>();
    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonitoredService>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).HasMaxLength(50);
            entity.Property(s => s.Name).HasMaxLength(100);
            entity.Property(s => s.Description).HasMaxLength(500);
            entity.Property(s => s.Status).HasConversion<string>();
        });
        modelBuilder.Entity<HealthCheckRecord>(entity =>
        {
            entity.HasIndex(h => new { h.ServiceId, h.CheckedAt });
            entity.HasIndex(h => h.CheckedAt);
            entity.Property(h => h.Status).HasConversion<string>();
            entity.HasOne(h => h.Service).WithMany().HasForeignKey(h => h.ServiceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasIndex(i => i.CreatedAt);
            entity.Property(i => i.Title).HasMaxLength(120);
            entity.Property(i => i.Description).HasMaxLength(1000);
            entity.Property(i => i.Resolution).HasMaxLength(1000);
            entity.Property(i => i.Severity).HasConversion<string>();
            entity.Property(i => i.Status).HasConversion<string>();
            entity.HasOne(i => i.Service).WithMany().HasForeignKey(i => i.ServiceId).OnDelete(DeleteBehavior.Restrict);
        });
        // SQLite does not preserve DateTime.Kind. All persisted timestamps are UTC.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            if (property.ClrType == typeof(DateTime))
                property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                    v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
            if (property.ClrType == typeof(DateTime?))
                property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
                    v => v.HasValue ? v.Value.ToUniversalTime() : v,
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v));
        }
    }
}
