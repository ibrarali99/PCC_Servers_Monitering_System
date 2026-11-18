using Kataria.IT.Data;
using Kataria.IT.Models;
using Kataria.IT.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
const long maxUpload = 250L * 1024 * 1024;

// ---- Database (database-first: tables are created by Database/KatariaITMonitoring.sql) ----
builder.Services.AddDbContext<KatariaDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---- MVC + CSRF protection on every POST ----
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

// ---- Authentication: cookie login against AppUsers table ----
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/Denied";
        o.Cookie.Name = "Kataria.IT.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;      // Lax is needed for the Google OAuth redirect back to /Drive/Callback
        o.ExpireTimeSpan = TimeSpan.FromHours(10);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(o =>
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());   // everything needs login unless [AllowAnonymous]

// Keep encryption keys on disk so login cookies / the saved Google token survive restarts.
var keyDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Keys");
Directory.CreateDirectory(keyDir);
builder.Services.AddDataProtection().SetApplicationName("Kataria.IT").PersistKeysToFileSystem(new DirectoryInfo(keyDir));

// ---- Upload limits (many phone photos per report) ----
builder.Services.Configure<FormOptions>(o => { o.MultipartBodyLengthLimit = maxUpload; o.ValueCountLimit = 10000; });
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxUpload);

// ---- Application services ----
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IGoogleDriveService, GoogleDriveService>();
builder.Services.AddSingleton<IEvidenceStorage, EvidenceStorage>();
builder.Services.AddSingleton<IExcelReportBuilder>(sp => new ExcelReportBuilder(sp.GetRequiredService<IEvidenceStorage>()));

var app = builder.Build();

// First-run safety net: if AppUsers is empty, create the admin from appsettings (BootstrapAdmin).
// The SQL script already inserts adminpcc, so normally this does nothing.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<KatariaDbContext>();
        if (!await db.AppUsers.AnyAsync())
        {
            var u = app.Configuration["BootstrapAdmin:Username"] ?? "adminpcc";
            var p = app.Configuration["BootstrapAdmin:Password"] ?? "admin@123";
            db.AppUsers.Add(new AppUser { Username = u, DisplayName = "Administrator", Role = "Admin", PasswordHash = PasswordHasher.Hash(p), CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            logger.LogWarning("No users found - created admin '{User}'. Change the password after login.", u);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Could not reach the database. Check ConnectionStrings:DefaultConnection and run Database/KatariaITMonitoring.sql.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
