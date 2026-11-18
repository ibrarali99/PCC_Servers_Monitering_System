using Kataria.IT.Services;
using Kataria.IT.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kataria.IT.Controllers;

[Authorize(Roles = "Admin")]
public class DriveController(IGoogleDriveService drive, ILogger<DriveController> log) : Controller
{
    private const string StateCookie = "Kataria.IT.OAuthState";
    private string RedirectUri => Url.Action(nameof(Callback), "Drive", null, Request.Scheme, Request.Host.Value)!;

    public async Task<IActionResult> Index()
    {
        var s = await drive.GetStatusAsync();
        return View(new DriveSettingsVm
        {
            HasClientCredentials = s.HasClientCredentials, Connected = s.Connected,
            AccountEmail = s.AccountEmail, RootFolderId = s.RootFolderId, RedirectUri = RedirectUri
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save(DriveSettingsVm vm)
    {
        if (string.IsNullOrWhiteSpace(vm.ClientId))
        {
            TempData["Error"] = "Client ID zaroori hai.";
            return RedirectToAction(nameof(Index));
        }
        var st = await drive.GetStatusAsync();
        if (string.IsNullOrWhiteSpace(vm.ClientSecret) && !st.HasClientCredentials)
        {
            TempData["Error"] = "Client Secret zaroori hai.";
            return RedirectToAction(nameof(Index));
        }
        await drive.SaveClientCredentialsAsync(vm.ClientId, vm.ClientSecret ?? "", vm.RootFolderId);
        TempData["Success"] = "Google settings save ho gayin. Ab 'Connect Google Drive' dabayein.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Connect()
    {
        try
        {
            var state = Guid.NewGuid().ToString("N");
            Response.Cookies.Append(StateCookie, state, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps, MaxAge = TimeSpan.FromMinutes(10) });
            return Redirect(await drive.BuildAuthUrlAsync(RedirectUri, state));
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    // Google redirects the browser back here (GET).
    [HttpGet]
    public async Task<IActionResult> Callback(string? code, string? state, string? error)
    {
        if (!string.IsNullOrEmpty(error)) { TempData["Error"] = "Google ne access nahi diya: " + error; return RedirectToAction(nameof(Index)); }
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) || Request.Cookies[StateCookie] != state)
        {
            TempData["Error"] = "Connect request invalid thi (state mismatch). Dobara try karein.";
            return RedirectToAction(nameof(Index));
        }
        Response.Cookies.Delete(StateCookie);
        try
        {
            await drive.ConnectAsync(code, RedirectUri);
            TempData["Success"] = "Google Drive connect ho gaya.";
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Drive connect failed");
            TempData["Error"] = "Connect nahi ho saka: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Test()
    {
        try { TempData["Success"] = $"Drive folder theek hai: \"{await drive.TestRootFolderAsync()}\" – upload ke liye tayyar."; }
        catch (Exception ex) { TempData["Error"] = "Drive test fail: " + ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Disconnect()
    {
        await drive.DisconnectAsync();
        TempData["Success"] = "Google Drive disconnect kar diya gaya.";
        return RedirectToAction(nameof(Index));
    }
}
