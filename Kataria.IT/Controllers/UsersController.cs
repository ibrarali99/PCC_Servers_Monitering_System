using System.Security.Claims;
using Kataria.IT.Data;
using Kataria.IT.Models;
using Kataria.IT.Services;
using Kataria.IT.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kataria.IT.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController(KatariaDbContext db) : Controller
{
    private int CurrentId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<UsersVm> LoadAsync(CreateUserVm? nu = null) => new()
    {
        Users = await db.AppUsers.AsNoTracking().OrderBy(u => u.Username).ToListAsync(),
        NewUser = nu ?? new()
    };

    public async Task<IActionResult> Index() => View(await LoadAsync());

    [HttpPost]
    public async Task<IActionResult> Create([Bind(Prefix = "NewUser")] CreateUserVm vm)
    {
        if (vm.Role is not ("Admin" or "Staff")) ModelState.AddModelError("NewUser.Role", "Invalid role selected");
        var username = vm.Username?.Trim() ?? "";
        if (await db.AppUsers.AnyAsync(u => u.Username == username)) ModelState.AddModelError("NewUser.Username", "This username already exists. Please choose a different username");
        if (!ModelState.IsValid) return View(nameof(Index), await LoadAsync(vm));

        db.AppUsers.Add(new AppUser
        {
            Username = username, DisplayName = vm.DisplayName.Trim(), Role = vm.Role,
            PasswordHash = PasswordHasher.Hash(vm.Password), CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        TempData["Success"] = $"User '{username}' Create Successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(int id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            TempData["Error"] = "New password must be at least 6 characters long";
            return RedirectToAction(nameof(Index));
        }
        var u = await db.AppUsers.FindAsync(id);
        if (u is null) return NotFound();
        u.PasswordHash = PasswordHasher.Hash(newPassword);
        u.FailedLoginCount = 0; u.LockoutEndUtc = null;
        await db.SaveChangesAsync();
        TempData["Success"] = $"'{u.Username}' Password has been reset successfully";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        var u = await db.AppUsers.FindAsync(id);
        if (u is null) return NotFound();
        if (u.UserId == CurrentId) { TempData["Error"] = "You cannot disable your own account"; return RedirectToAction(nameof(Index)); }
        u.IsActive = !u.IsActive;
        await db.SaveChangesAsync();
        TempData["Success"] = $"'{u.Username}' ab {(u.IsActive ? "active" : "disabled")} hai.";
        return RedirectToAction(nameof(Index));
    }
}
