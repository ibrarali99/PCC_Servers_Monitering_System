namespace Kataria.IT.Models;

// NOTE: These classes mirror the SQL tables (database-first).
// To regenerate them from the database run the scaffold command in README.md.

public partial class AppUser
{
    public int UserId { get; set; }
    public string Username { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Role { get; set; } = "Staff";
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public partial class Server
{
    public int ServerId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string IpAddress { get; set; } = null!;
    public string Location { get; set; } = null!;
    public string Role { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public partial class CheckItem
{
    public int CheckItemId { get; set; }
    public string Code { get; set; } = null!;
    public string Label { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public partial class DailyReport
{
    public int ReportId { get; set; }
    public DateOnly ReportDate { get; set; }
    public string Shift { get; set; } = "Daily inspection";
    public int? PreparedByUserId { get; set; }
    public string PreparedByName { get; set; } = null!;
    public string? ReviewedBy { get; set; }
    public string RoomCondition { get; set; } = "Not Checked";
    public string ManagerNotification { get; set; } = "No";
    public string InternetStatus { get; set; } = "Not Checked";
    public string? InternetNotes { get; set; }
    public string UpsStatus { get; set; } = "Not Checked";
    public string? UpsNotes { get; set; }
    public string? Summary { get; set; }
    public string OverallStatus { get; set; } = "OK";
    public string DriveSyncStatus { get; set; } = "Pending";
    public string? DriveSyncMessage { get; set; }
    public string? DriveFolderId { get; set; }
    public string? DriveFolderLink { get; set; }
    public string? DriveExcelFileId { get; set; }
    public string? DriveExcelLink { get; set; }
    public DateTime? DriveSyncedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public virtual AppUser? PreparedByUser { get; set; }
    public virtual ICollection<ServerEntry> ServerEntries { get; set; } = new List<ServerEntry>();
    public virtual ICollection<EvidenceFile> EvidenceFiles { get; set; } = new List<EvidenceFile>();
}

public partial class ServerEntry
{
    public int ServerEntryId { get; set; }
    public int ReportId { get; set; }
    public int ServerId { get; set; }
    public string? Notes { get; set; }

    public virtual DailyReport Report { get; set; } = null!;
    public virtual Server Server { get; set; } = null!;
    public virtual ICollection<ServerCheckResult> CheckResults { get; set; } = new List<ServerCheckResult>();
}

public partial class ServerCheckResult
{
    public int ServerEntryId { get; set; }
    public int CheckItemId { get; set; }
    public string Status { get; set; } = "Not Checked";

    public virtual ServerEntry ServerEntry { get; set; } = null!;
    public virtual CheckItem CheckItem { get; set; } = null!;
}

public partial class EvidenceFile
{
    public int EvidenceId { get; set; }
    public int ReportId { get; set; }
    public int? ServerId { get; set; }
    public string Category { get; set; } = null!;   // Server / Internet / Ups
    public string OriginalName { get; set; } = null!;
    public string StoredName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string? DriveFileId { get; set; }
    public string? DriveLink { get; set; }
    public string DriveStatus { get; set; } = "Pending";
    public DateTime UploadedAtUtc { get; set; }

    public virtual DailyReport Report { get; set; } = null!;
    public virtual Server? Server { get; set; }
}

public partial class AppSetting
{
    public string SettingKey { get; set; } = null!;
    public string? SettingValue { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
