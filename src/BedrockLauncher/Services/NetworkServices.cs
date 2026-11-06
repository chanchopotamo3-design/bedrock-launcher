using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using BedrockLauncher.Core;
namespace BedrockLauncher.Services;

public static class Net
{
    public static readonly HttpClient Http = Create();
    static HttpClient Create() { var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) }; c.DefaultRequestHeaders.UserAgent.ParseAdd("BedrockUltimateLauncher/1.0"); return c; }
    public static Uri RequireHttps(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException("Solo se permiten URLs https.");
        return u;
    }
}

/// <summary>Notas de versión oficiales (estable + Preview/Beta) desde el sitio oficial de Minecraft (Mojang).</summary>
public class NewsService
{
    static readonly Regex Ver = new(@"\d+\.\d+(?:\.\d+){1,2}", RegexOptions.Compiled);
    public async Task<List<NewsItem>> GetAsync(string url, CancellationToken ct = default)
    {
        var u = Net.RequireHttps(url);
        if (!u.Host.EndsWith("minecraft.net")) throw new InvalidOperationException("Fuente no autorizada: " + u.Host);
        using var resp = await Net.Http.GetAsync(u, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var list = new List<NewsItem>();
        foreach (var a in doc.RootElement.GetProperty("articles").EnumerateArray())
        {
            var title = a.GetProperty("title").GetString() ?? "";
            var m = Ver.Match(title);
            if (!m.Success) continue;
            list.Add(new NewsItem
            {
                Title = title, Version = m.Value, Url = a.GetProperty("html_url").GetString() ?? "",
                Date = a.TryGetProperty("created_at", out var c) && c.TryGetDateTime(out var dt) ? dt.ToLocalTime() : DateTime.MinValue,
                IsPreview = title.Contains("Preview", StringComparison.OrdinalIgnoreCase) || title.Contains("Beta", StringComparison.OrdinalIgnoreCase)
            });
        }
        return list.OrderByDescending(n => n.Date).ToList();
    }
}

public record UpdateInfo(string Version, string PageUrl, string? DownloadUrl);

/// <summary>Actualizaciones del propio launcher (GitHub Releases) con descarga cancelable y lista blanca de dominios.</summary>
public class UpdateService
{
    static readonly string[] AllowedHosts = { "github.com", "githubusercontent.com", "microsoft.com" };
    public static Version Current => System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);

    public async Task<UpdateInfo?> CheckAsync(string url, CancellationToken ct)
    {
        var u = Net.RequireHttps(url);
        using var resp = await Net.Http.GetAsync(u, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var r = doc.RootElement;
        var tag = (r.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
        if (!Version.TryParse(tag, out var v) || v <= Current) return null;
        string? dl = null;
        if (r.TryGetProperty("assets", out var assets))
            foreach (var a in assets.EnumerateArray())
            {
                var n = a.GetProperty("name").GetString() ?? "";
                if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) { dl = a.GetProperty("browser_download_url").GetString(); break; }
            }
        return new UpdateInfo(tag, r.GetProperty("html_url").GetString() ?? "", dl);
    }

    public async Task<string> DownloadAsync(string url, IProgress<double> progress, CancellationToken ct)
    {
        var u = Net.RequireHttps(url);
        if (!AllowedHosts.Any(h => u.Host == h || u.Host.EndsWith("." + h))) throw new InvalidOperationException("Fuente no autorizada: " + u.Host);
        Directory.CreateDirectory(AppPaths.Downloads);
        var dest = Path.Combine(AppPaths.Downloads, FileUtil.SafeName(Path.GetFileName(u.LocalPath)));
        try
        {
            using var resp = await Net.Http.GetAsync(u, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? -1;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(dest);
            var buf = new byte[81920]; long read = 0; int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0) { await dst.WriteAsync(buf.AsMemory(0, n), ct); read += n; if (total > 0) progress.Report(read * 100.0 / total); }
        }
        catch { try { File.Delete(dest); } catch { } throw; }
        return dest;
    }
}
