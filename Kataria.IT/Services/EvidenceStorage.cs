namespace Kataria.IT.Services;

public record StoredEvidence(string StoredName, string ContentType, long Size, string OriginalName);

public interface IEvidenceStorage
{
    /// <summary>Validates (extension + file signature + size) and stores an uploaded photo. Returns null if rejected.</summary>
    Task<StoredEvidence?> SaveAsync(IFormFile file);
    Stream? Open(string storedName);
    byte[]? ReadAll(string storedName);
    void Delete(string storedName);
}

public class EvidenceStorage(IWebHostEnvironment env) : IEvidenceStorage
{
    public const long MaxBytes = 15 * 1024 * 1024;
    private readonly string _root = Path.Combine(env.ContentRootPath, "App_Data", "Evidence");

    private static readonly Dictionary<string, string> Types = new()
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp"
    };

    public async Task<StoredEvidence?> SaveAsync(IFormFile file)
    {
        if (file.Length == 0 || file.Length > MaxBytes) return null;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Types.TryGetValue(ext, out var contentType)) return null;

        // Check real file signature, not just the extension.
        var head = new byte[12];
        await using (var s = file.OpenReadStream())
        {
            var read = await s.ReadAsync(head.AsMemory(0, 12));
            if (read < 12 || !HasValidSignature(head, ext)) return null;
        }

        var sub = DateTime.UtcNow.ToString("yyyyMM");
        var stored = $"{sub}/{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(_root, sub, Path.GetFileName(stored));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using (var fs = File.Create(full)) await file.CopyToAsync(fs);
        return new StoredEvidence(stored, contentType, file.Length, Path.GetFileName(file.FileName));
    }

    private static bool HasValidSignature(byte[] h, string ext) => ext switch
    {
        ".jpg" or ".jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
        ".png" => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
        ".webp" => h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P',
        _ => false
    };

    private string? Resolve(string storedName)
    {
        var full = Path.GetFullPath(Path.Combine(_root, storedName));
        // Guard against path traversal.
        return full.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase) && File.Exists(full) ? full : null;
    }

    public Stream? Open(string storedName) => Resolve(storedName) is { } p ? File.OpenRead(p) : null;
    public byte[]? ReadAll(string storedName) => Resolve(storedName) is { } p ? File.ReadAllBytes(p) : null;
    public void Delete(string storedName) { if (Resolve(storedName) is { } p) File.Delete(p); }
}
