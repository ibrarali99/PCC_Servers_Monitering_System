using System.ComponentModel.DataAnnotations;
using Kataria.IT.Models;

namespace Kataria.IT.ViewModels;

public class LoginVm
{
    [Required(ErrorMessage = "Username likhein")] public string Username { get; set; } = "";
    [Required(ErrorMessage = "Password likhein")] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class ChangePasswordVm
{
    [Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = "";
    [Required, MinLength(6, ErrorMessage = "Kam az kam 6 characters")] [DataType(DataType.Password)] public string NewPassword { get; set; } = "";
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords match nahi karte")] [DataType(DataType.Password)] public string ConfirmPassword { get; set; } = "";
}

public class ServerInputVm
{
    public int ServerId { get; set; }
    public string? Notes { get; set; }
    /// <summary>key = CheckItem.Code, value = Not Checked / OK / Issue / N/A</summary>
    public Dictionary<string, string> Checks { get; set; } = new();
    public List<IFormFile> Photos { get; set; } = new();
}

public class NewReportVm
{
    [Required] public DateOnly ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Shift { get; set; } = "Daily inspection";
    [Required(ErrorMessage = "Prepared by likhein")] public string PreparedBy { get; set; } = "";
    public string? ReviewedBy { get; set; }
    public string RoomCondition { get; set; } = "Not Checked";
    public string ManagerNotification { get; set; } = "No";
    public string InternetStatus { get; set; } = "Not Checked";
    public string? InternetNotes { get; set; }
    public string UpsStatus { get; set; } = "Not Checked";
    public string? UpsNotes { get; set; }
    public string? Summary { get; set; }
    public List<ServerInputVm> Servers { get; set; } = new();
    public List<IFormFile> InternetPhotos { get; set; } = new();
    public List<IFormFile> UpsPhotos { get; set; } = new();

    // Display only (not posted)
    public List<Server> ServerList { get; set; } = new();
    public List<CheckItem> CheckItems { get; set; } = new();
}

public class ReportFilterVm
{
    public DateOnly? Date { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? ServerId { get; set; }
    public string? Status { get; set; }
    public List<DailyReport> Results { get; set; } = new();
    public List<Server> Servers { get; set; } = new();
}

public class DashboardVm
{
    public DailyReport? TodayReport { get; set; }
    public int ServerCount { get; set; }
    public int TotalReports { get; set; }
    public int TotalPhotos { get; set; }
    public int IssueReportsLast30 { get; set; }
    public List<DailyReport> Recent { get; set; } = new();
    public bool DriveConnected { get; set; }
    public string? DriveAccount { get; set; }
}

public class CreateUserVm
{
    [Required, StringLength(50)] public string Username { get; set; } = "";
    [Required, StringLength(100)] public string DisplayName { get; set; } = "";
    [Required, MinLength(6)] public string Password { get; set; } = "";
    public string Role { get; set; } = "Staff";
}

public class UsersVm
{
    public List<AppUser> Users { get; set; } = new();
    public CreateUserVm NewUser { get; set; } = new();
}

public class DriveSettingsVm
{
    public bool HasClientCredentials { get; set; }
    public bool Connected { get; set; }
    public string? AccountEmail { get; set; }
    public string RootFolderId { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
