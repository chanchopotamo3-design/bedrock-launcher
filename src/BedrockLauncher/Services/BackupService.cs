using System.IO.Compression;
using BedrockLauncher.Core;
namespace BedrockLauncher.Services;

/// <summary>Copias de seguridad .zip de la carpeta com.mojang (mundos, packs, skins, options.txt).</summary>
public class BackupService
{
    readonly AppSettings _s;
    public BackupService(AppSettings s) { _s = s; }
    public string Dir => string.IsNullOrWhiteSpace(_s.BackupDir) ? AppPaths.DefaultBackups : _s.BackupDir;

    public Task<BackupInfo> CreateAsync(InstallInfo inst, string label, IProgress<double>? progress, CancellationToken ct) => Task.Run(() =>
    {
        if (!Directory.Exists(inst.DataPath)) throw new DirectoryNotFoundException(inst.DataPath);
        Directory.CreateDirectory(Dir);
        var path = Path.Combine(Dir, $"{FileUtil.SafeName(label)}_{inst.Channel}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        var files = new DirectoryInfo(inst.DataPath).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).ToList();
        long total = Math.Max(1, files.Sum(f => f.Length)), done = 0;
        try
        {
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                foreach (var f in files)
                {
                    ct.ThrowIfCancellationRequested();
                    var rel = Path.GetRelativePath(inst.DataPath, f.FullName).Replace('\\', '/');
                    try
                    {
                        var e = zip.CreateEntry(rel, CompressionLevel.Optimal);
                        using var src = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var dst = e.Open(); src.CopyTo(dst);
                    }
                    catch (IOException ex) { Log.Error($"Omitido (bloqueado) {f.FullName}", ex); }
                    done += f.Length; progress?.Report(done * 100.0 / total);
                }
            }
        }
        catch { try { File.Delete(path); } catch { } throw; }
        Prune();
        return Info(path);
    }, ct);

    public Task RestoreAsync(BackupInfo b, InstallInfo inst, IProgress<double>? progress, CancellationToken ct) => Task.Run(() =>
    {
        if (FileUtil.IsGameRunning()) throw new InvalidOperationException(Localizer.Instance["msg_game_running"]);
        var root = Path.GetFullPath(inst.DataPath) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(b.Path);
        int i = 0;
        foreach (var e in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (e.FullName.EndsWith('/')) { i++; continue; }
            var target = Path.GetFullPath(Path.Combine(inst.DataPath, e.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Entrada insegura en la copia: " + e.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            e.ExtractToFile(target, true);
            progress?.Report(++i * 100.0 / zip.Entries.Count);
        }
    }, ct);

    public List<BackupInfo> List()
    {
        if (!Directory.Exists(Dir)) return new();
        return Directory.EnumerateFiles(Dir, "*.zip").Select(Info).OrderByDescending(b => b.Date).ToList();
    }

    public void Delete(BackupInfo b) => File.Delete(b.Path);

    static BackupInfo Info(string p)
    {
        var fi = new FileInfo(p);
        return new BackupInfo { Path = p, Date = fi.LastWriteTime, Size = fi.Length, Channel = p.Contains("_Preview_") ? Channel.Preview : Channel.Release };
    }

    void Prune()
    {
        var max = Math.Max(1, _s.MaxBackups);
        foreach (var old in List().Skip(max)) { try { File.Delete(old.Path); } catch (Exception ex) { Log.Error("Prune", ex); } }
    }
}
