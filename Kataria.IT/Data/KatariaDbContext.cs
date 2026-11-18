using Kataria.IT.Models;
using Microsoft.EntityFrameworkCore;

namespace Kataria.IT.Data;

public partial class KatariaDbContext(DbContextOptions<KatariaDbContext> options) : DbContext(options)
{
    public virtual DbSet<AppUser> AppUsers { get; set; }
    public virtual DbSet<Server> Servers { get; set; }
    public virtual DbSet<CheckItem> CheckItems { get; set; }
    public virtual DbSet<DailyReport> DailyReports { get; set; }
    public virtual DbSet<ServerEntry> ServerEntries { get; set; }
    public virtual DbSet<ServerCheckResult> ServerCheckResults { get; set; }
    public virtual DbSet<EvidenceFile> EvidenceFiles { get; set; }
    public virtual DbSet<AppSetting> AppSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.ToTable("AppUsers"); e.HasKey(x => x.UserId);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(50);
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.Property(x => x.PasswordHash).HasMaxLength(300);
            e.Property(x => x.Role).HasMaxLength(20);
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.LockoutEndUtc).HasColumnType("datetime2(0)");
            e.Property(x => x.LastLoginUtc).HasColumnType("datetime2(0)");
        });

        b.Entity<Server>(e =>
        {
            e.ToTable("Servers"); e.HasKey(x => x.ServerId);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.IpAddress).HasMaxLength(45).IsUnicode(false);
        });

        b.Entity<CheckItem>(e =>
        {
            e.ToTable("CheckItems"); e.HasKey(x => x.CheckItemId);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        });

        b.Entity<DailyReport>(e =>
        {
            e.ToTable("DailyReports"); e.HasKey(x => x.ReportId);
            e.HasIndex(x => x.ReportDate);
            e.Property(x => x.ReportDate).HasColumnType("date");
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.DriveSyncedAtUtc).HasColumnType("datetime2(0)");
            e.HasOne(x => x.PreparedByUser).WithMany().HasForeignKey(x => x.PreparedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ServerEntry>(e =>
        {
            e.ToTable("ServerEntries"); e.HasKey(x => x.ServerEntryId);
            e.HasIndex(x => new { x.ReportId, x.ServerId }).IsUnique();
            e.HasOne(x => x.Report).WithMany(r => r.ServerEntries).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Server).WithMany().HasForeignKey(x => x.ServerId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        b.Entity<ServerCheckResult>(e =>
        {
            e.ToTable("ServerCheckResults");
            e.HasKey(x => new { x.ServerEntryId, x.CheckItemId });
            e.HasOne(x => x.ServerEntry).WithMany(s => s.CheckResults).HasForeignKey(x => x.ServerEntryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CheckItem).WithMany().HasForeignKey(x => x.CheckItemId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        b.Entity<EvidenceFile>(e =>
        {
            e.ToTable("EvidenceFiles"); e.HasKey(x => x.EvidenceId);
            e.Property(x => x.Category).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.UploadedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.Report).WithMany(r => r.EvidenceFiles).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Server).WithMany().HasForeignKey(x => x.ServerId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        b.Entity<AppSetting>(e =>
        {
            e.ToTable("AppSettings"); e.HasKey(x => x.SettingKey);
            e.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }
}
