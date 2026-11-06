using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BedrockLauncher.Core;
namespace BedrockLauncher.Services;

/// <summary>Detecta instalaciones legítimas de Bedrock (Microsoft Store), las lanza y las desinstala.
/// Solo LEE el registro de paquetes de Windows; nunca toca archivos de licencia ni del paquete.</summary>
public class InstallationService
{
    static readonly (Channel ch, string pkg, string fam)[] Defs =
    {
        (Channel.Release, "Microsoft.MinecraftUWP", "Microsoft.MinecraftUWP_8wekyb3d8bbwe"),
        (Channel.Preview, "Microsoft.MinecraftWindowsBeta", "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe"),
    };
    public const string StoreRelease = "ms-windows-store://pdp/?productid=9NBLGGH2JHXJ";
    public const string StorePreview = "ms-windows-store://pdp/?productid=9P5X4QVLC2XR";

    public async Task<List<InstallInfo>> DetectAsync()
    {
        var res = new List<InstallInfo>();
        foreach (var d in Defs)
        {
            var info = new InstallInfo
            {
                Channel = d.ch, Family = d.fam, PackageName = d.pkg,
                DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", d.fam, "LocalState", "games", "com.mojang")
            };
            try
            {
                var json = await RunPowerShellAsync($"Get-AppxPackage -Name {d.pkg} | Select-Object -First 1 @{{n='Version';e={{$_.Version.ToString()}}}},InstallLocation,PackageFullName | ConvertTo-Json -Compress");
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var doc = JsonDocument.Parse(json);
                    var r = doc.RootElement;
                    info.Installed = true;
                    info.Version = GameVersion.FromPackage(r.GetProperty("Version").GetString());
                    info.FullName = r.GetProperty("PackageFullName").GetString();
                    info.InstallLocation = r.GetProperty("InstallLocation").GetString();
                    if (Directory.Exists(info.InstallLocation)) info.InstalledOn = Directory.GetCreationTime(info.InstallLocation!);
                }
            }
            catch (Exception ex) { Log.Error($"Detección de {d.pkg}", ex); }
            info.DataSize = await Task.Run(() => FileUtil.DirSize(info.DataPath));
            res.Add(info);
        }
        return res;
    }

    public void Launch(InstallInfo i)
    {
        if (!i.Installed) throw new InvalidOperationException(Localizer.Instance["msg_not_installed"]);
        Process.Start(new ProcessStartInfo("explorer.exe", $"shell:appsFolder\\{i.AppId}") { UseShellExecute = true });
    }

    public static void OpenStore(Channel c) =>
        Process.Start(new ProcessStartInfo(c == Channel.Release ? StoreRelease : StorePreview) { UseShellExecute = true });

    /// <summary>Desinstala el paquete para el usuario actual (la licencia de la cuenta no se modifica).</summary>
    public async Task UninstallAsync(InstallInfo i)
    {
        if (string.IsNullOrEmpty(i.FullName) || !Regex.IsMatch(i.FullName, @"^[A-Za-z0-9._~\-]+$")) throw new InvalidOperationException("Nombre de paquete inválido.");
        if (FileUtil.IsGameRunning()) throw new InvalidOperationException(Localizer.Instance["msg_game_running"]);
        await RunPowerShellAsync($"Remove-AppxPackage -Package '{i.FullName}'");
    }

    static async Task<string> RunPowerShellAsync(string script)
    {
        var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -EncodedCommand {enc}")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("No se pudo iniciar PowerShell");
        var outTask = p.StandardOutput.ReadToEndAsync(); var errTask = p.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await p.WaitForExitAsync(cts.Token); } catch (OperationCanceledException) { try { p.Kill(true); } catch { } throw new TimeoutException("PowerShell tardó demasiado"); }
        var err = await errTask;
        if (p.ExitCode != 0 && !string.IsNullOrWhiteSpace(err)) throw new InvalidOperationException(err.Trim());
        return (await outTask).Trim();
    }
}
