using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Kataria.IT.Data;
using Kataria.IT.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace Kataria.IT.Services;

public record DriveStatusInfo(bool HasClientCredentials, bool Connected, string? AccountEmail, string RootFolderId);
public record DriveItem(string Id, string? Link);

public interface IGoogleDriveService
{
    Task<DriveStatusInfo> GetStatusAsync();
    Task SaveClientCredentialsAsync(string clientId, string clientSecret, string? rootFolderId);
    Task<string> BuildAuthUrlAsync(string redirectUri, string state);
    Task ConnectAsync(string code, string redirectUri);
    Task DisconnectAsync();
    Task<bool> IsConnectedAsync();
    Task<string> TestRootFolderAsync();
    /// <summary>Finds (or creates) a sub-folder inside the configured root folder.</summary>
    Task<DriveItem> EnsureFolderAsync(string name);
    Task<DriveItem> UploadAsync(string parentFolderId, string fileName, string contentType, byte[] data, string? existingFileId = null);
}

/// <summary>
/// Uploads to a normal (personal) Google account using OAuth. A service account cannot be used for a
/// personal Gmail "My Drive" because service accounts have no storage quota.
/// </summary>
public class GoogleDriveService(KatariaDbContext db, IConfiguration config, IDataProtectionProvider dp) : IGoogleDriveService
{
    private const string KClientId = "Drive.ClientId", KClientSecret = "Drive.ClientSecret",
                         KRefresh = "Drive.RefreshToken", KEmail = "Drive.AccountEmail", KRoot = "Drive.RootFolderId";
    private static readonly string[] Scopes = [DriveService.Scope.Drive];
    private readonly IDataProtector _protector = dp.CreateProtector("Kataria.IT.GoogleDrive.v1");

    // ---------- settings storage ----------
    private async Task<string?> GetAsync(string key, bool secret = false)
    {
        var v = (await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.SettingKey == key))?.SettingValue;
        if (string.IsNullOrEmpty(v)) return null;
        if (!secret) return v;
        try { return _protector.Unprotect(v); } catch { return null; }   // keys changed => treat as not set
    }

    private async Task SetAsync(string key, string? value, bool secret = false)
    {
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
        if (value is null) { if (row != null) db.AppSettings.Remove(row); }
        else
        {
            var stored = secret ? _protector.Protect(value) : value;
            if (row is null) db.AppSettings.Add(new AppSetting { SettingKey = key, SettingValue = stored, UpdatedAtUtc = DateTime.UtcNow });
            else { row.SettingValue = stored; row.UpdatedAtUtc = DateTime.UtcNow; }
        }
        await db.SaveChangesAsync();
    }

    private async Task<string> RootFolderIdAsync() =>
        await GetAsync(KRoot) ?? config["GoogleDrive:RootFolderId"] ?? "";

    private async Task<(string? Id, string? Secret)> ClientAsync() =>
        (await GetAsync(KClientId) ?? config["GoogleDrive:ClientId"],
         await GetAsync(KClientSecret, true) ?? config["GoogleDrive:ClientSecret"]);

    private async Task<GoogleAuthorizationCodeFlow> FlowAsync()
    {
        var (id, secret) = await ClientAsync();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Google Client ID / Secret set nahi hain (Admin → Google Drive).");
        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = id, ClientSecret = secret },
            Scopes = Scopes,
            DataStore = new NullDataStore()
        });
    }

    private async Task<DriveService> ServiceAsync()
    {
        var refresh = await GetAsync(KRefresh, true) ?? throw new InvalidOperationException("Google Drive connect nahi hai.");
        var flow = await FlowAsync();
        var credential = new UserCredential(flow, "kataria", new TokenResponse { RefreshToken = refresh });
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Kataria IT Monitoring"
        });
    }

    // ---------- connection management ----------
    public async Task<DriveStatusInfo> GetStatusAsync()
    {
        var (id, secret) = await ClientAsync();
        var connected = await GetAsync(KRefresh, true) != null;
        return new DriveStatusInfo(!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(secret), connected,
                                   await GetAsync(KEmail), await RootFolderIdAsync());
    }

    public async Task<bool> IsConnectedAsync() => (await GetStatusAsync()) is { HasClientCredentials: true, Connected: true };

    public async Task SaveClientCredentialsAsync(string clientId, string clientSecret, string? rootFolderId)
    {
        await SetAsync(KClientId, clientId.Trim());
        if (!string.IsNullOrWhiteSpace(clientSecret)) await SetAsync(KClientSecret, clientSecret.Trim(), true);
        if (!string.IsNullOrWhiteSpace(rootFolderId)) await SetAsync(KRoot, rootFolderId.Trim());
    }

    public async Task<string> BuildAuthUrlAsync(string redirectUri, string state)
    {
        var (id, _) = await ClientAsync();
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Pehle Client ID save karein.");
        string E(string s) => Uri.EscapeDataString(s);
        return "https://accounts.google.com/o/oauth2/v2/auth" +
               $"?client_id={E(id)}&redirect_uri={E(redirectUri)}&response_type=code" +
               $"&scope={E(Scopes[0])}&access_type=offline&prompt=consent&state={E(state)}";
    }

    public async Task ConnectAsync(string code, string redirectUri)
    {
        var flow = await FlowAsync();
        var token = await flow.ExchangeCodeForTokenAsync("kataria", code, redirectUri, CancellationToken.None);
        if (string.IsNullOrEmpty(token.RefreshToken))
            throw new InvalidOperationException("Google ne refresh token nahi diya. Google Account → Security → Third-party access se app remove karke dobara connect karein.");
        await SetAsync(KRefresh, token.RefreshToken, true);

        var service = await ServiceAsync();
        var about = service.About.Get();
        about.Fields = "user(emailAddress,displayName)";
        var info = await about.ExecuteAsync();
        await SetAsync(KEmail, info.User?.EmailAddress);
    }

    public async Task DisconnectAsync()
    {
        await SetAsync(KRefresh, null);
        await SetAsync(KEmail, null);
    }

    public async Task<string> TestRootFolderAsync()
    {
        var service = await ServiceAsync();
        var get = service.Files.Get(await RootFolderIdAsync());
        get.Fields = "id,name,mimeType,capabilities(canAddChildren)";
        get.SupportsAllDrives = true;
        var f = await get.ExecuteAsync();
        if (f.MimeType != "application/vnd.google-apps.folder") throw new InvalidOperationException("Yeh ID folder ki nahi hai.");
        if (f.Capabilities?.CanAddChildren == false) throw new InvalidOperationException("Is folder mein file add karne ki permission nahi hai.");
        return f.Name;
    }

    // ---------- folders & files ----------
    public async Task<DriveItem> EnsureFolderAsync(string name)
    {
        var service = await ServiceAsync();
        var root = await RootFolderIdAsync();
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Drive root folder ID set nahi hai.");

        var list = service.Files.List();
        list.Q = $"name = '{name.Replace("'", "\\'")}' and '{root}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        list.Fields = "files(id,webViewLink)";
        list.SupportsAllDrives = true;
        list.IncludeItemsFromAllDrives = true;
        var found = (await list.ExecuteAsync()).Files.FirstOrDefault();
        if (found != null) return new DriveItem(found.Id, found.WebViewLink);

        var create = service.Files.Create(new DriveFile
        {
            Name = name, MimeType = "application/vnd.google-apps.folder", Parents = [root]
        });
        create.Fields = "id,webViewLink";
        create.SupportsAllDrives = true;
        var folder = await create.ExecuteAsync();
        return new DriveItem(folder.Id, folder.WebViewLink);
    }

    public async Task<DriveItem> UploadAsync(string parentFolderId, string fileName, string contentType, byte[] data, string? existingFileId = null)
    {
        var service = await ServiceAsync();
        using var stream = new MemoryStream(data);

        if (!string.IsNullOrEmpty(existingFileId))
        {
            try
            {
                var update = service.Files.Update(new DriveFile { Name = fileName }, existingFileId, stream, contentType);
                update.Fields = "id,webViewLink";
                update.SupportsAllDrives = true;
                var up = await update.UploadAsync();
                if (up.Status == UploadStatus.Completed) return new DriveItem(update.ResponseBody.Id, update.ResponseBody.WebViewLink);
            }
            catch { /* file may have been deleted in Drive – fall through and create a new one */ }
            stream.Position = 0;
        }

        var create = service.Files.Create(new DriveFile { Name = fileName, Parents = [parentFolderId] }, stream, contentType);
        create.Fields = "id,webViewLink";
        create.SupportsAllDrives = true;
        var result = await create.UploadAsync();
        if (result.Status != UploadStatus.Completed)
            throw result.Exception ?? new InvalidOperationException($"Upload {result.Status}");
        return new DriveItem(create.ResponseBody.Id, create.ResponseBody.WebViewLink);
    }
}
