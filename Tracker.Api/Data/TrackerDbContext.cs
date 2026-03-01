using Microsoft.EntityFrameworkCore;
using Tracker.Api.Models;

namespace Tracker.Api.Data;

public class TrackerDbContext : DbContext
{
    public TrackerDbContext(DbContextOptions<TrackerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<IngestCursor> IngestCursors => Set<IngestCursor>();
    public DbSet<WebEvent> WebEvents => Set<WebEvent>();
    public DbSet<WebSession> WebSessions => Set<WebSession>();
    public DbSet<AppSession> AppSessions => Set<AppSession>();
    public DbSet<IdleSession> IdleSessions => Set<IdleSession>();
    public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();
    public DbSet<MonitorSession> MonitorSessions => Set<MonitorSession>();
    public DbSet<Screenshot> Screenshots => Set<Screenshot>();
    public DbSet<IdleSecondsRow> IdleSecondsRows => Set<IdleSecondsRow>();
    public DbSet<DomainSummaryRow> DomainSummaryRows => Set<DomainSummaryRow>();
    public DbSet<AppSummaryRow> AppSummaryRows => Set<AppSummaryRow>();
    public DbSet<UrlSummaryRow> UrlSummaryRows => Set<UrlSummaryRow>();
    public DbSet<DeviceOnBoundsRow> DeviceOnBoundsRows => Set<DeviceOnBoundsRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>()
            .HasIndex(d => d.LastSeenAt);
        modelBuilder.Entity<Device>()
            .HasIndex(d => d.CompanyId);
        modelBuilder.Entity<Device>()
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Company>()
            .HasIndex(c => c.EnrollmentKeyHash)
            .IsUnique();
        modelBuilder.Entity<Company>()
            .Property(c => c.Name)
            .HasMaxLength(200);
        modelBuilder.Entity<Company>()
            .Property(c => c.EnrollmentKey)
            .HasMaxLength(256);
        modelBuilder.Entity<Company>()
            .Property(c => c.EnrollmentKeyHash)
            .HasMaxLength(128);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasMaxLength(320);
        modelBuilder.Entity<User>()
            .Property(u => u.PasswordHash)
            .HasMaxLength(512);
        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasMaxLength(64);
        modelBuilder.Entity<User>()
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(u => u.CompanyId);

        modelBuilder.Entity<IngestCursor>()
            .HasKey(c => new { c.DeviceId, c.Stream });

        modelBuilder.Entity<WebEvent>()
            .HasIndex(e => e.EventId)
            .IsUnique();
        modelBuilder.Entity<WebEvent>()
            .HasIndex(e => new { e.DeviceId, e.Timestamp });
        modelBuilder.Entity<WebEvent>()
            .Property(e => e.EventId)
            .IsRequired();
        modelBuilder.Entity<WebEvent>()
            .Property(e => e.DeviceId)
            .IsRequired()
            .HasMaxLength(64);
        modelBuilder.Entity<WebEvent>()
            .Property(e => e.Domain)
            .IsRequired()
            .HasMaxLength(255);
        modelBuilder.Entity<WebEvent>()
            .Property(e => e.Title)
            .HasMaxLength(512);
        modelBuilder.Entity<WebEvent>()
            .Property(e => e.Url)
            .HasMaxLength(2048);
        modelBuilder.Entity<WebEvent>()
            .Property(e => e.Browser)
            .HasMaxLength(64);
        modelBuilder.Entity<WebEvent>()
            .Property(e => e.VideoUrl)
            .HasMaxLength(2048);
        modelBuilder.Entity<WebEvent>()
            .Property(e => e.VideoDomain)
            .HasMaxLength(255);
        modelBuilder.Entity<WebEvent>()
            .Property(e => e.ReceivedAt)
            .IsRequired();

        modelBuilder.Entity<WebSession>()
            .HasIndex(s => new { s.DeviceId, s.StartAt });
        modelBuilder.Entity<WebSession>()
            .HasIndex(s => new { s.DeviceId, s.EndAt });
        modelBuilder.Entity<WebSession>()
            .HasIndex(s => s.SessionId)
            .IsUnique();
        modelBuilder.Entity<WebSession>()
            .Property(s => s.SessionId)
            .IsRequired();
        modelBuilder.Entity<WebSession>()
            .Property(s => s.Url)
            .HasMaxLength(2048);
        modelBuilder.Entity<WebSession>()
            .Property(s => s.Domain)
            .IsRequired()
            .HasMaxLength(255);
        modelBuilder.Entity<WebSession>()
            .Property(s => s.Title)
            .HasMaxLength(512);

        modelBuilder.Entity<AppSession>()
            .HasIndex(s => new { s.DeviceId, s.StartAt });
        modelBuilder.Entity<AppSession>()
            .HasIndex(s => new { s.DeviceId, s.EndAt });
        modelBuilder.Entity<AppSession>()
            .HasIndex(s => s.SessionId)
            .IsUnique();
        modelBuilder.Entity<AppSession>()
            .Property(s => s.SessionId)
            .IsRequired();

        modelBuilder.Entity<IdleSession>()
            .HasIndex(s => new { s.DeviceId, s.StartAt });
        modelBuilder.Entity<IdleSession>()
            .HasIndex(s => new { s.DeviceId, s.EndAt });
        modelBuilder.Entity<IdleSession>()
            .HasIndex(s => s.SessionId)
            .IsUnique();
        modelBuilder.Entity<IdleSession>()
            .Property(s => s.SessionId)
            .IsRequired();

        modelBuilder.Entity<DeviceSession>()
            .HasIndex(s => new { s.DeviceId, s.StartAt });
        modelBuilder.Entity<DeviceSession>()
            .HasIndex(s => new { s.DeviceId, s.EndAt });
        modelBuilder.Entity<DeviceSession>()
            .HasIndex(s => s.SessionId)
            .IsUnique();
        modelBuilder.Entity<DeviceSession>()
            .Property(s => s.SessionId)
            .IsRequired();

        modelBuilder.Entity<MonitorSession>()
            .HasIndex(s => new { s.DeviceId, s.Timestamp });
        modelBuilder.Entity<MonitorSession>()
            .HasIndex(s => new { s.DeviceId, s.MonitorId, s.Timestamp });
        modelBuilder.Entity<MonitorSession>()
            .HasIndex(s => s.SessionId)
            .IsUnique();
        modelBuilder.Entity<MonitorSession>()
            .Property(s => s.SessionId)
            .IsRequired();
        modelBuilder.Entity<MonitorSession>()
            .Property(s => s.MonitorId)
            .HasMaxLength(128);
        modelBuilder.Entity<MonitorSession>()
            .Property(s => s.ActiveWindowProcess)
            .HasMaxLength(255);
        modelBuilder.Entity<MonitorSession>()
            .Property(s => s.ActiveWindowTitle)
            .HasMaxLength(512);

        modelBuilder.Entity<Screenshot>()
            .HasIndex(s => new { s.DeviceId, s.Timestamp });
        modelBuilder.Entity<Screenshot>()
            .HasIndex(s => s.ScreenshotId)
            .IsUnique();
        modelBuilder.Entity<Screenshot>()
            .Property(s => s.ScreenshotId)
            .IsRequired();
        modelBuilder.Entity<Screenshot>()
            .Property(s => s.MonitorId)
            .HasMaxLength(128);
        modelBuilder.Entity<Screenshot>()
            .Property(s => s.FilePath)
            .HasMaxLength(2048);
        modelBuilder.Entity<Screenshot>()
            .Property(s => s.TriggerReason)
            .HasMaxLength(128);

        modelBuilder.Entity<IdleSecondsRow>().HasNoKey();
        modelBuilder.Entity<DomainSummaryRow>().HasNoKey();
        modelBuilder.Entity<AppSummaryRow>().HasNoKey();
        modelBuilder.Entity<UrlSummaryRow>().HasNoKey();
        modelBuilder.Entity<DeviceOnBoundsRow>().HasNoKey();
    }
}
