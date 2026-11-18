using System.Security.Claims;
using Kataria.IT.Services;
using Kataria.IT.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kataria.IT.Data;

namespace Kataria.IT.Controllers;

public class ReportsController(IReportService reports, IExcelReportBuilder excel, IEvidenceStorage storage, KatariaDbContext db) : Controller
{
    private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";
    private string DisplayName => User.FindFirstValue("DisplayName") ?? User.Identity?.Name ?? "";

    // ---------- list / date-wise search ----------
    public async Task<IActionResult> Index(ReportFilterVm f)
    {
        f.Servers = await db.Servers.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync();
        f.Results = await reports.SearchAsync(f);
        return View(f);
    }

    // ---------- new report ----------
    [HttpGet]
    public async Task<IActionResult> Create() => View(await reports.NewFormAsync(DisplayName));

    [HttpPost]
    [RequestSizeLimit(262144000), RequestFormLimits(MultipartBodyLengthLimit = 262144000)]
    public async Task<IActionResult> Create(NewReportVm vm)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjax)
                return BadRequest(new { error = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)) });
            await reports.FillListsAsync(vm);
            return View(vm);
        }

        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var report = await reports.CreateAsync(vm, userId);
            var url = Url.Action(nameof(Details), new { id = report.ReportId })!;
            if (IsAjax) return Json(new { ok = true, redirect = url, drive = report.DriveSyncStatus });
            TempData["Success"] = "Report save ho gayi.";
            return Redirect(url);
        }
        catch (Exception ex)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<ReportsController>>().LogError(ex, "Report save failed");
            if (IsAjax) return StatusCode(500, new { error = "Report save nahi ho saki: " + ex.Message });
            ModelState.AddModelError("", "Report save nahi ho saki: " + ex.Message);
            await reports.FillListsAsync(vm);
            return View(vm);
        }
    }

    // ---------- details ----------
    public async Task<IActionResult> Details(int id)
    {
        var r = await reports.GetAsync(id);
        return r is null ? NotFound() : View(r);
    }

    // ---------- photo (only for logged in users) ----------
    public async Task<IActionResult> Evidence(int id)
    {
        var ev = await db.EvidenceFiles.AsNoTracking().FirstOrDefaultAsync(e => e.EvidenceId == id);
        if (ev is null) return NotFound();
        var stream = storage.Open(ev.StoredName);
        return stream is null ? NotFound() : File(stream, ev.ContentType);
    }

    // ---------- Excel download (generated on demand) ----------
    public async Task<IActionResult> Excel(int id)
    {
        var r = await reports.GetAsync(id);
        if (r is null) return NotFound();
        return File(excel.Build(r), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"IT-Daily-Report_{r.ReportDate:yyyy-MM-dd}_{r.ReportId}.xlsx");
    }

    [HttpPost]
    public async Task<IActionResult> Resync(int id)
    {
        await reports.SyncToDriveAsync(id);
        TempData["Success"] = "Google Drive sync dobara chalaya gaya. Neeche status dekhein.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await reports.DeleteAsync(id);
        TempData["Success"] = "Report delete ho gayi (Google Drive ki copy wahan mojood rahegi).";
        return RedirectToAction(nameof(Index));
    }
}
