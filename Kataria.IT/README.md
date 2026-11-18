# Kataria IT Monitoring – ASP.NET Core MVC (Database-First)

ASP.NET Core 10 MVC · SQL Server (database-first) · Bootstrap 5 · Excel report (ClosedXML) · Google Drive upload

**Login:** `adminpcc` / `admin@123` (login ke baad Password change kar lein)

## Folder structure
```
Database/KatariaITMonitoring.sql   <- poora database (tables + seed data + admin user)
Models/                            <- tables ke entity classes (scaffold-style)
Data/KatariaDbContext.cs           <- EF Core DbContext
Services/                          <- AuthService, ReportService, ExcelReportBuilder, GoogleDriveService, EvidenceStorage
Controllers/                       <- Account, Home, Reports, Users, Drive
ViewModels/  Views/  wwwroot/      <- UI (Bootstrap + JS)
```

## Setup (4 steps)
1. **Database:** SSMS mein `Database/KatariaITMonitoring.sql` poora run karein (dobara run karna bhi safe hai).
   Tables: AppUsers, Servers, CheckItems, DailyReports, ServerEntries, ServerCheckResults, EvidenceFiles, AppSettings.
   *ASP.NET Identity ab use nahi hoti – purani Identity tables ki zaroorat nahi.*
2. **Connection string:** `appsettings.json` → `ConnectionStrings:DefaultConnection` (default `Server=.` = isi PC ka SQL Server; apne server ka naam likhein).
3. **Run:** Visual Studio mein `Kataria.IT.csproj` open karke F5, ya `dotnet run`.
4. **Google Drive:** app mein Admin → **Google Drive** page kholein aur wahan likhe steps follow karein (OAuth Client ID/Secret + Connect).

## Database-first: models dobara generate karna
Agar kal ko SQL table badlein to:
```
dotnet tool install --global dotnet-ef
dotnet ef dbcontext scaffold "Server=.;Database=KatariaITMonitoring;Trusted_Connection=True;TrustServerCertificate=True" Microsoft.EntityFrameworkCore.SqlServer -o Models --context-dir Data --context KatariaDbContext --force --no-onconfiguring
```
(Is se `Models/` aur `Data/KatariaDbContext.cs` overwrite ho jayenge.)

## Roz ka kaam
1. **New Report** → date, 5 servers ke checks, har server ki photos, Internet aur UPS ki photos → **Save & Upload to Drive**.
2. Report SQL Server mein save hoti hai, phir Excel (`IT-Daily-Report_yyyy-MM-dd_...xlsx`, photos embedded) aur saari photos Drive folder
   `1k9G1hhCYtkdQCIQvePulPlmRwnFUF_qn` ke andar us din ke sub-folder (`2026-11-18`) mein upload hoti hain.
3. **Report History** mein date / date-range / server / status se purani report dekh sakte hain, Excel download ya print kar sakte hain.
4. Agar Drive upload fail ho to report phir bhi SQL mein save rehti hai; Report page par **"Drive par dobara upload"** button hai.

## Google Drive ke baare mein zaroori baatein
* `ibrarali.pcc@gmail.com` personal Gmail hai, is liye service-account kaam nahi karta; app **OAuth** use karti hai. Connect karte waqt usi Gmail se login karein.
* OAuth consent screen ko **In production** karein, warna Google ka token 7 din mein expire ho jata hai.
* Redirect URI Google par exact register karna hota hai (Drive page par dikhai deti hai). Google LAN IP / server-name allow nahi karta, is liye **Connect** server wali machine par `https://localhost:PORT` se dabayein. Sirf ek baar karna hai; token DB mein encrypted save hota hai.
* App sirf Drive mein file add/update karti hai. Report delete karne par Drive ki copy delete nahi hoti.

## Company-wide deployment
IIS / Windows Server par publish karein (`dotnet publish -c Release`), HTTPS lagayein, aur employees ko internal URL dein. `App_Data/Evidence` (photos) aur `App_Data/Keys` ka backup rakhein, SQL Server ka bhi. `web.config` mein upload limit 250 MB set hai.

## Security notes
* Passwords PBKDF2-SHA256 (600k iterations) se hash hote hain; 5 ghalat koshishon par 10 minute lockout.
* Har POST par CSRF token; saray pages login ke baghair band; photos sirf logged-in users dekh sakte hain.
* Google Client Secret aur refresh token database mein Data-Protection se encrypted hain.
