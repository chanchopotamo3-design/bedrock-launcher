using System.Windows;
using System.Windows.Media;
using BedrockLauncher.Core;
using BedrockLauncher.Services;
using BedrockLauncher.UI;
namespace BedrockLauncher;

public partial class App : Application
{
    static Mutex? _mutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "BedrockUltimateLauncher.SingleInstance", out var first);
        if (!first) { MessageBox.Show("Bedrock Ultimate Launcher ya está en ejecución.", "Launcher"); Shutdown(); return; }
        AppPaths.EnsureAll();
        DispatcherUnhandledException += (_, a) => { Log.Error("Excepción no controlada", a.Exception); MessageBox.Show(a.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); a.Handled = true; };
        TaskScheduler.UnobservedTaskException += (_, a) => { Log.Error("Tarea no observada", a.Exception); a.SetObserved(); };
        var s = new SettingsService().Load();
        if (s.SoftwareRender) System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        base.OnStartup(e);
        ThemeManager.Apply(s);
    }
}
