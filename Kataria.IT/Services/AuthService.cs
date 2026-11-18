using Kataria.IT.Data;
using Kataria.IT.Models;
using Microsoft.EntityFrameworkCore;

namespace Kataria.IT.Services;

public interface IAuthService
{
    Task<(AppUser? User, string? Error)> ValidateAsync(string username, string password);
}

public class AuthService(KatariaDbContext db) : IAuthService
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(10);

    public async Task<(AppUser? User, string? Error)> ValidateAsync(string username, string password)
    {
        username = (username ?? "").Trim();
        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Username == username);
        const string invalid = "Username ya password ghalat hai.";
        if (user is null) return (null, invalid);
        if (!user.IsActive) return (null, "Yeh account disable hai. Administrator se rabta karein.");
        if (user.LockoutEndUtc is { } end && end > DateTime.UtcNow)
            return (null, $"Bohat ziada ghalat koshishein. {Math.Ceiling((end - DateTime.UtcNow).TotalMinutes)} minute baad dobara try karein.");

        if (!PasswordHasher.Verify(password ?? "", user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailures)
            {
                user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutTime);
                user.FailedLoginCount = 0;
            }
            await db.SaveChangesAsync();
            return (null, invalid);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (user, null);
    }
}
