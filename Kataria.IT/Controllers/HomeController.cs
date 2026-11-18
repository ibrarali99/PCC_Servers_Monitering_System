using System.Diagnostics;
using Kataria.IT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kataria.IT.Controllers;

public class HomeController(IReportService reports) : Controller
{
    public async Task<IActionResult> Index() => View(await reports.DashboardAsync());

    [AllowAnonymous]
    public IActionResult Error()
    {
        ViewBag.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
