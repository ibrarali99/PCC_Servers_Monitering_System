using System.Security.Claims;
using Kataria.IT.Data;
using Kataria.IT.Services;
using Kataria.IT.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kataria.IT.Controllers;

public class AccountController(IAuthService auth, KatariaDbContext db) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var (user, error) = await auth.ValidateAsync(vm.Username, vm.Password);
        if (user is null)
        {
            ModelState.AddModelError("", error ?? "Login nakam.");
            vm.Password = "";
            return View(vm);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("DisplayName", user.DisplayName),
            new(ClaimTypes.Role, user.Role)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(principal, new AuthenticationProperties { IsPersistent = vm.RememberMe });

        return !string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)
            ? LocalRedirect(vm.ReturnUrl) : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult Denied() => View();

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordVm());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.AppUsers.FindAsync(id);
        if (user is null || !PasswordHasher.Verify(vm.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(vm.CurrentPassword), "Maujooda password ghalat hai.");
            return View(vm);
        }
        user.PasswordHash = PasswordHasher.Hash(vm.NewPassword);
        await db.SaveChangesAsync();
        TempData["Success"] = "Password change ho gaya.";
        return RedirectToAction("Index", "Home");
    }
}
