using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;          // ← esta línea la agregas tú
namespace BedrockLauncher.Services;

public record ImportResult(List<string> Imported, List<string> Errors);
public record ManifestData(string Name, string Description, string Uuid, string Version, Version? MinEngine, List<string> Modules, List<string> Dependencies);

/// <summary>Escaneo, importación, activación, desactivación y borrado de add-ons, packs, skins y mundos.
/// "Desactivar" mueve la carpeta fuera de com.mojang a una carpeta del launcher (reversible, sin perder nada).</summary>
public class ContentService
{
    public static readonly string[] Folders = { "development_behavior_packs", "development_resource_packs", "skin_packs", "minecraftWorlds", "world_templates" };
    static readonly JsonDocumentOptions JOpt = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    // ---------- Escaneo ----------
    public List<ContentItem> Scan(InstallInfo inst)
    {
        var list = new List<ContentItem>();
        foreach (var sub in Folders)
        {
            ScanDir(Path.Combine(inst.DataPath, sub), sub, true, list);
            ScanDir(Path.Combine(AppPaths.Disabled, inst.Channel.ToString(), sub), sub, false, list);
        }
        Validate(list, inst.Version);
        return list.OrderBy(i => i.Kind).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    void ScanDir(string dir, string sub, bool enabled, List<ContentItem> list)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            try { var it = Read(d, sub, enabled); if (it != null) list.Add(it); }
            catch (Exception ex) { Log.Error($"Leyendo {d}", ex); }
        }
    }

    ContentItem? Read(string dir, string sub, bool enabled)
    {
        var item = new ContentItem { Path = dir, SubFolder = sub, Enabled = enabled, SizeBytes = FileUtil.DirSize(dir) };
        if (sub == "minecraftWorlds")
        {
            item.Kind = ContentKind.World;
            var lv = System.IO.Path.Combine(dir, "levelname.txt");
            item.Name = File.Exists(lv) ? File.ReadAllText(lv).Trim() : System.IO.Path.GetFileName(dir);
            item.Description = "";
            item.IconPath = FirstExisting(dir, "world_icon.jpeg", "world_icon.jpg", "world_icon.png");
            return item;
        }
        var mf = System.IO.Path.Combine(dir, "manifest.json");
        if (!File.Exists(mf)) return null;
        var m = ReadManifest(mf);
        item.Name = Resolve(dir, m.Name); item.Description = Resolve(dir, m.Description);
        item.Uuid = m.Uuid; item.Version = m.Version; item.MinEngine = m.MinEngine; item.Dependencies = m.Dependencies;
        item.Kind = sub switch { "development_behavior_packs" => ContentKind.Behavior, "development_resource_packs" => ContentKind.Resource, "skin_packs" => ContentKind.Skin, _ => ContentKind.Template };
        if (item.Kind == ContentKind.Resource)
            item.IsShader = Directory.Exists(System.IO.Path.Combine(dir, "materials")) || Directory.EnumerateFiles(dir, "*.material.bin", SearchOption.AllDirectories).Any();
        item.IconPath = FirstExisting(dir, "pack_icon.png", "pack_icon.jpg", "world_icon.jpeg");
        return item;
    }

    static string? FirstExisting(string dir, params string[] names) => names.Select(n => System.IO.Path.Combine(dir, n)).FirstOrDefault(File.Exists);

    /// <summary>Lee manifest.json tolerando comentarios y comas finales (muy comunes en add-ons).</summary>
    public static ManifestData ReadManifest(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file), JOpt);
        var root = doc.RootElement;
        var h = root.GetProperty("header");
        string Str(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        string Ver(JsonElement e, string n) => e.TryGetProperty(n, out var v) ? v.ValueKind == JsonValueKind.Array ? string.Join('.', v.EnumerateArray().Select(x => x.ToString())) : v.ToString() : "";
        Version? min = null;
        var minStr = Ver(h, "min_engine_version");
        if (minStr.Split('.').Length == 3 && Version.TryParse(minStr, out var mv)) min = mv;
        var modules = new List<string>();
        if (root.TryGetProperty("modules", out var mods) && mods.ValueKind == JsonValueKind.Array)
            foreach (var m in mods.EnumerateArray()) modules.Add(Str(m, "type").ToLowerInvariant());
        var deps = new List<string>();
        if (root.TryGetProperty("dependencies", out var ds) && ds.ValueKind == JsonValueKind.Array)
            foreach (var d in ds.EnumerateArray()) { var u = Str(d, "uuid"); if (u.Length > 0) deps.Add(u.ToLowerInvariant()); }
        return new ManifestData(Str(h, "name"), Str(h, "description"), Str(h, "uuid").ToLowerInvariant(), Ver(h, "version"), min, modules, deps);
    }

    /// <summary>Resuelve claves de localización (pack.name) desde texts/en_US.lang o es_ES.lang.</summary>
    static string Resolve(string dir, string val)
    {
        if (string.IsNullOrEmpty(val) || val.Contains(' ') || !val.Contains('.')) return val;
        foreach (var lang in new[] { "en_US.lang", "es_ES.lang", "en_GB.lang" })
        {
            var f = System.IO.Path.Combine(dir, "texts", lang);
            if (!File.Exists(f)) continue;
            foreach (var line in File.ReadLines(f))
                if (line.StartsWith(val + "=")) return line[(val.Length + 1)..].Split('\t')[0].Split("##")[0].Trim();
        }
        return val;
    }

    /// <summary>Detecta incompatibilidades: min_engine_version mayor que el juego instalado y dependencias ausentes.</summary>
    void Validate(List<ContentItem> items, string? gameVersion)
    {
        var game = GameVersion.ForCompare(gameVersion);
        var active = items.Where(i => i.Enabled && i.Uuid.Length > 0).Select(i => i.Uuid).ToHashSet();
        foreach (var i in items)
        {
            var w = new List<string>();
            if (game != null && i.MinEngine != null && i.MinEngine > game) w.Add($"⚠ Requiere Minecraft {i.MinEngine} o superior (tienes {game}).");
            if (i.Enabled) foreach (var dep in i.Dependencies) if (!active.Contains(dep)) w.Add("⚠ Falta o está desactivada una dependencia.");
            i.Warning = string.Join(" ", w.Distinct());
        }
    }

    // ---------- Importación ----------
    public Task<ImportResult> ImportAsync(string file, InstallInfo inst, CancellationToken ct) => Task.Run(() =>
    {
        var res = new ImportResult(new(), new());
        var tmp = System.IO.Path.Combine(AppPaths.Temp, Guid.NewGuid().ToString("N"));
        try { ImportCore(file, tmp, inst, res, 0, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { res.Errors.Add($"{System.IO.Path.GetFileName(file)}: {ex.Message}"); Log.Error("Importación", ex); }
        finally { try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } }
        return res;
    }, ct);

    void ImportCore(string file, string tmp, InstallInfo inst, ImportResult res, int depth, CancellationToken ct)
    {
        if (depth > 3) throw new InvalidDataException("Paquete anidado demasiado profundo.");
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(tmp);
        ZipFile.ExtractToDirectory(file, tmp, true); // .NET rechaza rutas fuera del destino (zip-slip)

        if (File.Exists(System.IO.Path.Combine(tmp, "level.dat"))) { ImportWorld(tmp, inst, res); return; }

        // paquetes anidados (.mcaddon que contiene .mcpack/.mcworld)
        foreach (var nested in Directory.EnumerateFiles(tmp, "*.*", SearchOption.AllDirectories)
                     .Where(f => new[] { ".mcpack", ".mcworld", ".mcaddon", ".mctemplate" }.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant())).ToList())
            ImportCore(nested, System.IO.Path.Combine(tmp, "_n" + Guid.NewGuid().ToString("N")[..6]), inst, res, depth + 1, ct);

        var roots = Directory.EnumerateFiles(tmp, "manifest.json", SearchOption.AllDirectories).Select(f => System.IO.Path.GetDirectoryName(f)!)
            .Where(d => !d.Contains(System.IO.Path.DirectorySeparatorChar + "_n")).ToList();
        var outer = roots.Where(r => !roots.Any(o => o != r && r.StartsWith(o + System.IO.Path.DirectorySeparatorChar))).ToList();
        if (outer.Count == 0 && res.Imported.Count == 0) throw new InvalidDataException("No se encontró manifest.json ni level.dat: archivo no compatible.");
        foreach (var dir in outer) { ct.ThrowIfCancellationRequested(); ImportPack(dir, inst, res); }
    }

    void ImportWorld(string dir, InstallInfo inst, ImportResult res)
    {
        var lv = System.IO.Path.Combine(dir, "levelname.txt");
        var name = File.Exists(lv) ? File.ReadAllText(lv).Trim() : "World";
        var dest = System.IO.Path.Combine(inst.DataPath, "minecraftWorlds", FileUtil.SafeName(name) + "_" + Guid.NewGuid().ToString("N")[..6]);
        FileUtil.CopyDir(dir, dest);
        res.Imported.Add(name);
    }

    void ImportPack(string dir, InstallInfo inst, ImportResult res)
    {
        var m = ReadManifest(System.IO.Path.Combine(dir, "manifest.json"));
        string sub;
        if (m.Modules.Contains("resources")) sub = "development_resource_packs";
        else if (m.Modules.Contains("skin_pack")) sub = "skin_packs";
        else if (m.Modules.Contains("world_template")) sub = "world_templates";
        else if (m.Modules.Any(t => t is "data" or "script" or "javascript" or "client_data")) sub = "development_behavior_packs";
        else if (m.Modules.Contains("interface")) sub = "development_resource_packs";
        else throw new InvalidDataException($"Tipo de módulo no soportado en '{m.Name}'.");
        var uuid8 = m.Uuid.Length >= 8 ? m.Uuid[..8] : Guid.NewGuid().ToString("N")[..8];
        var folder = FileUtil.SafeName(Resolve(dir, m.Name)) + "_" + uuid8;
        var dest = System.IO.Path.Combine(inst.DataPath, sub, folder);
        if (Directory.Exists(dest)) Directory.Delete(dest, true); // actualiza una versión previa
        var disabledCopy = System.IO.Path.Combine(AppPaths.Disabled, inst.Channel.ToString(), sub, folder);
        if (Directory.Exists(disabledCopy)) Directory.Delete(disabledCopy, true);
        FileUtil.CopyDir(dir, dest);
        res.Imported.Add(Resolve(dir, m.Name));
    }

    // ---------- Activar / desactivar / borrar ----------
    public void SetEnabled(ContentItem item, InstallInfo inst, bool enable)
    {
        if (!item.CanToggle) return;
        if (item.Enabled == enable) return;
        var name = System.IO.Path.GetFileName(item.Path);
        var active = System.IO.Path.Combine(inst.DataPath, item.SubFolder, name);
        var off = System.IO.Path.Combine(AppPaths.Disabled, inst.Channel.ToString(), item.SubFolder, name);
        var dest = enable ? active : off;
        FileUtil.MoveDir(item.Path, dest);
        item.Path = dest; item.Enabled = enable;
    }

    public void Delete(ContentItem item)
    {
        if (!Directory.Exists(item.Path)) return;
        Directory.Delete(item.Path, true);
    }
}
