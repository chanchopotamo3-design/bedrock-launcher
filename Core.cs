using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;

namespace BedrockLauncher.Core;

/// <summary>Rutas de datos del launcher (todo vive en %LOCALAPPDATA%, nunca en archivos protegidos del juego).</summary>
public static class AppPaths
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BedrockUltimateLauncher");
    public static string Settings => Path.Combine(Root, "settings.json");
    public static string Profiles => Path.Combine(Root, "profiles.json");
    public static string DefaultBackups => Path.Combine(Root, "Backups");
    public static string Disabled => Path.Combine(Root, "Disabled");
    public static string Temp => Path.Combine(Root, "Temp");
    public static string Downloads => Path.Combine(Root, "Downloads");
    public static string Logs => Path.Combine(Root, "Logs");
    public static void EnsureAll() { foreach (var p in new[] { Root, DefaultBackups, Disabled, Temp, Downloads, Logs }) Directory.CreateDirectory(p); }
}

/// <summary>Persistencia JSON atómica (escribe a .tmp y reemplaza).</summary>
public static class JsonStore
{
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static T Load<T>(string path, Func<T> def)
    {
        try { if (File.Exists(path)) return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Opt) ?? def(); }
        catch (Exception ex) { Log.Error($"No se pudo leer {path}", ex); }
        return def();
    }
    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Opt));
        File.Move(tmp, path, true);
    }
}

public static class Log
{
    static readonly object Gate = new();
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", ex == null ? msg : $"{msg}: {ex}");
    public static void Info(string msg) => Write("INFO", msg);
    static void Write(string level, string msg)
    {
        try { lock (Gate) { Directory.CreateDirectory(AppPaths.Logs); File.AppendAllText(Path.Combine(AppPaths.Logs, $"{DateTime.Now:yyyyMMdd}.log"), $"{DateTime.Now:HH:mm:ss} [{level}] {msg}{Environment.NewLine}"); } }
        catch { /* el log nunca debe romper la app */ }
    }
}

public static class GameVersion
{
    /// <summary>Convierte la versión del paquete (1.21.5001.0) en versión de juego (1.21.50.1).</summary>
    public static string FromPackage(string? pkg)
    {
        if (!Version.TryParse(pkg, out var v)) return pkg ?? "?";
        return $"{v.Major}.{v.Minor}.{v.Build / 100}.{v.Build % 100}";
    }
    /// <summary>Versión mayor.menor.parche para comparar con min_engine_version.</summary>
    public static Version? ForCompare(string? gameVersion)
    {
        if (!Version.TryParse(gameVersion, out var v)) return null;
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
    }
}

public static class FileUtil
{
    public static long DirSize(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
        try { return new DirectoryInfo(path).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Sum(f => f.Length); }
        catch { return 0; }
    }
    public static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
    }
    public static void MoveDir(string src, string dst)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        if (Directory.Exists(dst)) Directory.Delete(dst, true);
        try { Directory.Move(src, dst); }
        catch (IOException) { CopyDir(src, dst); Directory.Delete(src, true); } // distinto volumen
    }
    public static string SafeName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        s = s.Trim();
        return s.Length == 0 ? "pack" : s.Length > 60 ? s[..60] : s;
    }
    public static bool IsGameRunning() => Process.GetProcessesByName("Minecraft.Windows").Length > 0;
    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }
    public static string FormatBytes(long b)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" }; double v = b; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {u[i]}";
    }
}

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnChanged(name); return true;
    }
    protected void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand : ICommand
{
    readonly Action<object?> _run; readonly Func<object?, bool>? _can;
    public RelayCommand(Action<object?> run, Func<object?, bool>? can = null) { _run = run; _can = can; }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
    public void Execute(object? p) => _run(p);
}

public class AsyncCommand : ICommand
{
    readonly Func<object?, Task> _run; readonly Action<Exception> _onError; bool _running;
    public AsyncCommand(Func<object?, Task> run, Action<Exception> onError) { _run = run; _onError = onError; }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? p) => !_running;
    public async void Execute(object? p)
    {
        _running = true;
        try { await _run(p); } catch (OperationCanceledException) { } catch (Exception ex) { Log.Error("Comando falló", ex); _onError(ex); }
        finally { _running = false; CommandManager.InvalidateRequerySuggested(); }
    }
}
