using Kataria.IT.Data;
using Kataria.IT.Models;
using Kataria.IT.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Kataria.IT.Services;

public interface IReportService
{
    Task<NewReportVm> NewFormAsync(string preparedBy);
    Task FillListsAsync(NewReportVm vm);
    Task<DailyReport> CreateAsync(NewReportVm vm, int userId);
    Task<DailyReport?> GetAsync(int id);
    Task<List<DailyReport>> SearchAsync(ReportFilterVm f);
    Task<DashboardVm> DashboardAsync();
    Task SyncToDriveAsync(int reportId);
    Task DeleteAsync(int reportId);
}

public class ReportService(
    KatariaDbContext db,
    IEvidenceStorage storage,
    IExcelReportBuilder excel,
    IGoogleDriveService drive,
    ILogger<ReportService> log) : IReportService
{
    private static readonly HashSet<string> Statuses = new() { "Not Checked", "OK", "Issue", "N/A" };
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private IQueryable<DailyReport> Full() => db.DailyReports
        .Include(r => r.ServerEntries).ThenInclude(e => e.Server)
        .Include(r => r.ServerEntries).ThenInclude(e => e.CheckResults).ThenInclude(c => c.CheckItem)
        .Include(r => r.EvidenceFiles);

    public async Task FillListsAsync(NewReportVm vm)
    {
        vm.ServerList = await db.Servers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync();
        vm.CheckItems = await db.CheckItems.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
    }

    public async Task<NewReportVm> NewFormAsync(string preparedBy)
    {
        var vm = new NewReportVm { PreparedBy = preparedBy };
        await FillListsAsync(vm);
        return vm;
    }

    public async Task<DailyReport> CreateAsync(NewReportVm vm, int userId)
    {
        var servers = await db.Servers.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync();
        var items = await db.CheckItems.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();

        var report = new DailyReport
        {
            ReportDate = vm.ReportDate,
            Shift = Clean(vm.Shift, 30) ?? "Daily inspection",
            PreparedByUserId = userId,
            PreparedByName = Clean(vm.PreparedBy, 100) ?? "IT Staff",
            ReviewedBy = Clean(vm.ReviewedBy, 100),
            RoomCondition = Clean(vm.RoomCondition, 30) ?? "Not Checked",
            ManagerNotification = Clean(vm.ManagerNotification, 30) ?? "No",
            InternetStatus = Status(vm.InternetStatus),
            InternetNotes = Clean(vm.InternetNotes, 1000),
            UpsStatus = Status(vm.UpsStatus),
            UpsNotes = Clean(vm.UpsNotes, 1000),
            Summary = Clean(vm.Summary, 2000),
            CreatedAtUtc = DateTime.UtcNow,
            DriveSyncStatus = "Pending"
        };

        var savedFiles = new List<string>();
        try
        {
            foreach (var s in servers)
            {
                var input = vm.Servers.FirstOrDefault(x => x.ServerId == s.ServerId);
                var entry = new ServerEntry { ServerId = s.ServerId, Notes = Clean(input?.Notes, 1000) };
                foreach (var item in items)
                {
                    var status = input is not null && input.Checks.TryGetValue(item.Code, out var v) ? Status(v) : "Not Checked";
                    entry.CheckResults.Add(new ServerCheckResult { CheckItemId = item.CheckItemId, Status = status });
                }
                report.ServerEntries.Add(entry);
                await AddPhotosAsync(report, input?.Photos, "Server", s.ServerId, savedFiles);
            }
            await AddPhotosAsync(report, vm.InternetPhotos, "Internet", null, savedFiles);
            await AddPhotosAsync(report, vm.UpsPhotos, "Ups", null, savedFiles);

            var anyIssue = report.ServerEntries.Any(e => e.CheckResults.Any(c => c.Status == "Issue"))
                           || report.InternetStatus == "Issue" || report.UpsStatus == "Issue"
                           || report.RoomCondition is "Critical" or "Attention Required";
            report.OverallStatus = anyIssue ? "Attention Required" : "OK";

            db.DailyReports.Add(report);
            await db.SaveChangesAsync();
        }
        catch
        {
            foreach (var f in savedFiles) storage.Delete(f);   // do not leave orphan photos
            throw;
        }

        await SyncToDriveAsync(report.ReportId);
        return report;
    }

    private async Task AddPhotosAsync(DailyReport report, List<IFormFile>? files, string category, int? serverId, List<string> saved)
    {
        if (files is null) return;
        foreach (var f in files)
        {
            var stored = await storage.SaveAsync(f);
            if (stored is null) continue;
            saved.Add(stored.StoredName);
            report.EvidenceFiles.Add(new EvidenceFile
            {
                Category = category, ServerId = serverId, OriginalName = stored.OriginalName,
                StoredName = stored.StoredName, ContentType = stored.ContentType, SizeBytes = stored.Size,
                DriveStatus = "Pending", UploadedAtUtc = DateTime.UtcNow
            });
        }
    }

    public Task<DailyReport?> GetAsync(int id) => Full().AsSplitQuery().FirstOrDefaultAsync(r => r.ReportId == id);

    public async Task<List<DailyReport>> SearchAsync(ReportFilterVm f)
    {
        var q = db.DailyReports.AsNoTracking().Include(r => r.EvidenceFiles).AsQueryable();
        if (f.Date is { } d) q = q.Where(r => r.ReportDate == d);
        if (f.From is { } from) q = q.Where(r => r.ReportDate >= from);
        if (f.To is { } to) q = q.Where(r => r.ReportDate <= to);
        if (f.ServerId is { } sid) q = q.Where(r => r.ServerEntries.Any(e => e.ServerId == sid));
        if (!string.IsNullOrEmpty(f.Status)) q = q.Where(r => r.OverallStatus == f.Status);
        return await q.OrderByDescending(r => r.ReportDate).ThenByDescending(r => r.ReportId).Take(200).ToListAsync();
    }

    public async Task<DashboardVm> DashboardAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var since = today.AddDays(-30);
        var st = await drive.GetStatusAsync();
        return new DashboardVm
        {
            TodayReport = await db.DailyReports.AsNoTracking().Include(r => r.EvidenceFiles)
                                  .Where(r => r.ReportDate == today).OrderByDescending(r => r.ReportId).FirstOrDefaultAsync(),
            ServerCount = await db.Servers.CountAsync(x => x.IsActive),
            TotalReports = await db.DailyReports.CountAsync(),
            TotalPhotos = await db.EvidenceFiles.CountAsync(),
            IssueReportsLast30 = await db.DailyReports.CountAsync(r => r.ReportDate >= since && r.OverallStatus != "OK"),
            Recent = await db.DailyReports.AsNoTracking().Include(r => r.EvidenceFiles)
                             .OrderByDescending(r => r.ReportDate).ThenByDescending(r => r.ReportId).Take(7).ToListAsync(),
            DriveConnected = st.Connected && st.HasClientCredentials,
            DriveAccount = st.AccountEmail
        };
    }

    public async Task SyncToDriveAsync(int reportId)
    {
        var report = await GetAsync(reportId);
        if (report is null) return;

        if (!await drive.IsConnectedAsync())
        {
            report.DriveSyncStatus = "NotConfigured";
            report.DriveSyncMessage = "Google Drive connect nahi hai. Admin → Google Drive se connect karein, phir report mein 'Drive par dobara upload' dabayein.";
            await db.SaveChangesAsync();
            return;
        }

        try
        {
            var folder = await drive.EnsureFolderAsync(report.ReportDate.ToString("yyyy-MM-dd"));
            report.DriveFolderId = folder.Id;
            report.DriveFolderLink = folder.Link;

            var shift = string.Concat(report.Shift.Where(char.IsLetterOrDigit));
            var xlsx = excel.Build(report);
            var x = await drive.UploadAsync(folder.Id, $"IT-Daily-Report_{report.ReportDate:yyyy-MM-dd}_{shift}_{report.ReportId}.xlsx",
                                            XlsxType, xlsx, report.DriveExcelFileId);
            report.DriveExcelFileId = x.Id;
            report.DriveExcelLink = x.Link;

            int failed = 0, n = 0;
            foreach (var ev in report.EvidenceFiles.OrderBy(e => e.EvidenceId))
            {
                n++;
                if (ev.DriveStatus == "Uploaded") continue;
                try
                {
                    var bytes = storage.ReadAll(ev.StoredName) ?? throw new FileNotFoundException("Photo server par nahi mili.");
                    var label = ev.Category == "Server"
                        ? report.ServerEntries.FirstOrDefault(s => s.ServerId == ev.ServerId)?.Server.Code ?? "Server"
                        : ev.Category.ToUpperInvariant();
                    var up = await drive.UploadAsync(folder.Id, $"{label}_{report.ReportId}_{n}{Path.GetExtension(ev.StoredName)}", ev.ContentType, bytes);
                    ev.DriveFileId = up.Id; ev.DriveLink = up.Link; ev.DriveStatus = "Uploaded";
                }
                catch (Exception ex)
                {
                    failed++; ev.DriveStatus = "Failed";
                    log.LogWarning(ex, "Drive upload failed for evidence {Id}", ev.EvidenceId);
                }
            }

            report.DriveSyncStatus = failed == 0 ? "Uploaded" : "Partial";
            report.DriveSyncMessage = failed == 0 ? "Excel report aur photos Google Drive par upload ho gayin."
                                                   : $"{failed} photo(s) upload nahi hui. Dobara upload try karein.";
            report.DriveSyncedAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Drive sync failed for report {Id}", reportId);
            report.DriveSyncStatus = "Failed";
            report.DriveSyncMessage = Trim(ex.Message, 900);
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int reportId)
    {
        var report = await db.DailyReports.Include(r => r.EvidenceFiles).FirstOrDefaultAsync(r => r.ReportId == reportId);
        if (report is null) return;
        var files = report.EvidenceFiles.Select(e => e.StoredName).ToList();
        db.DailyReports.Remove(report);            // children removed by ON DELETE CASCADE
        await db.SaveChangesAsync();
        foreach (var f in files) storage.Delete(f);
    }

    private static string Status(string? s) => s is not null && Statuses.Contains(s) ? s : "Not Checked";
    private static string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : Trim(s.Trim(), max);
    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}
