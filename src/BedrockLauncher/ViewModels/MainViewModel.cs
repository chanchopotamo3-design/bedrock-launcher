using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using BedrockLauncher.Core;
using BedrockLauncher.Services;
using BedrockLauncher.UI;
using Microsoft.Win32;
namespace BedrockLauncher.ViewModels;

public class Notification { public string Text { get; set; } = ""; public bool IsError { get; set; } }

/// <summary>ViewModel único: orquesta servicios y expone estado/comandos a la UI (MVVM sin dependencias externas).</summary>
public class MainViewModel : ObservableObject
{
    readonly SettingsService _settingsSvc = new();
    readonly InstallationService _installSvc = new();
    readonly ContentService _content = new();
    readonly ProfileService _profileSvc = new();
    readonly NewsService _newsSvc = new();
    readonly UpdateService _updateSvc = new();
    readonly BackupService _backupSvc;
    CancellationTokenSource? _cts;

    public AppSettings Settings { get; }
    public Localizer L => Localizer.Instance;

    string _page = "home", _category = "cat_all", _search = "", _busyText = "", _newProfileName = "", _storageText = "", _updateText = "";
    bool _busy; double _progress; Channel _newChannel; UpdateInfo? _pendingUpdate; string? _bg;
    Profile? _selected; NewsItem? _latest;

    public ObservableCollection<InstallInfo> Installs { get; } = new();
    public ObservableCollection<ContentItem> ContentItems { get; } = new();
    public ICollectionView ContentView { get; }
    public ObservableCollection<BackupInfo> Backups { get; } = new();
    public ObservableCollection<Profile> Profiles { get; } = new();
    public ObservableCollection<NewsItem> Releases { get; } = new();
    public ObservableCollection<NewsItem> HomeNews { get; } = new();
    public ObservableCollection<Notification> Notifications { get; } = new();

    bool _compact;
    public bool IsCompact { get => _compact; set { if (Set(ref _compact, value)) { OnChanged(nameof(ShowLabels)); OnChanged(nameof(SidebarWidth)); } } }
    public bool ShowLabels => !_compact;
    public double SidebarWidth => _compact ? 64 : 200;
    public string CurrentPage { get => _page; set => Set(ref _page, value); }
    public string Category { get => _category; set { if (Set(ref _category, value)) ContentView.Refresh(); } }
    public string SearchText { get => _search; set { if (Set(ref _search, value)) { ContentView.Refresh(); if (value.Length > 0) CurrentPage = "content"; } } }
    public bool IsBusy { get => _busy; set => Set(ref _busy, value); }
    public string BusyText { get => _busyText; set => Set(ref _busyText, value); }
    public double Progress { get => _progress; set => Set(ref _progress, value); }
    public string NewProfileName { get => _newProfileName; set => Set(ref _newProfileName, value); }
    public string NewChannelName => _newChannel.ToString();
    public string StorageText { get => _storageText; set => Set(ref _storageText, value); }
    public string UpdateText { get => _updateText; set => Set(ref _updateText, value); }
    public bool UpdateAvailable => _pendingUpdate?.DownloadUrl != null;
    public string? BackgroundImage { get => _bg; set => Set(ref _bg, value); }
    public NewsItem? LatestRelease { get => _latest; set { Set(ref _latest, value); OnChanged(nameof(LatestText)); } }
    public string LatestText => LatestRelease == null ? "—" : $"{LatestRelease.Version}  ({LatestRelease.DateText})";
    public Profile? SelectedProfile { get => _selected; set { if (Set(ref _selected, value)) { foreach (var p in Profiles) p.IsActive = p == value; Settings.SelectedProfileId = value?.Id; OnChanged(nameof(CurrentInstall)); OnChanged(nameof(SelectedVersionText)); OnChanged(nameof(StatusText)); } } }
    public InstallInfo? CurrentInstall => Installs.FirstOrDefault(i => i.Channel == SelectedProfile?.Channel);
    public string SelectedVersionText => $"{SelectedProfile?.Name}  ·  {CurrentInstall?.VersionText}";
    public string StatusText => CurrentInstall == null ? "—" : $"{CurrentInstall.StatusText}  ·  {CurrentInstall.SizeText}";

    // ---- Comandos ----
    public ICommand NavigateCommand { get; }
    public ICommand OpenCategoryCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenContentFolderCommand { get; }
    public ICommand ToggleCommand { get; }
    public ICommand DeleteContentCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand DeleteBackupCommand { get; }
    public ICommand OpenBackupFolderCommand { get; }
    public ICommand StoreCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand OpenDataFolderCommand { get; }
    public ICommand OpenUrlCommand { get; }
    public ICommand SelectProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand SetNewChannelCommand { get; }
    public ICommand CreateProfileCommand { get; }
    public ICommand SetLanguageCommand { get; }
    public ICommand SetThemeCommand { get; }
    public ICommand ApplyAccentCommand { get; }
    public ICommand PickBackgroundCommand { get; }
    public ICommand ClearBackgroundCommand { get; }
    public ICommand PickBackupDirCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand CheckUpdatesCommand { get; }
    public ICommand DownloadUpdateCommand { get; }
    public ICommand CleanTempCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ReloadNewsCommand { get; }

    public MainViewModel()
    {
        Settings = _settingsSvc.Load();
        _backupSvc = new BackupService(Settings);
        L.SetLanguage(Settings.Language);
        _bg = Settings.BackgroundImage;
        ContentView = CollectionViewSource.GetDefaultView(ContentItems);
        ContentView.Filter = o => o is ContentItem c
            && (Category == "cat_all" || c.CategoryKey == Category)
            && (SearchText.Length == 0 || c.Name.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) || c.Description.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase));

        Action<Exception> err = ex => Notify(ex.Message, true);
        NavigateCommand = new RelayCommand(p => CurrentPage = (string)p!);
        OpenCategoryCommand = new RelayCommand(p => { Category = (string)p!; CurrentPage = "content"; });
        PlayCommand = new RelayCommand(_ => Play());
        ImportCommand = new AsyncCommand(_ => ImportAsync(), err);
        RefreshCommand = new AsyncCommand(_ => RefreshAllAsync(), err);
        OpenContentFolderCommand = new RelayCommand(_ => { if (CurrentInstall != null) FileUtil.OpenFolder(CurrentInstall.DataPath); });
        ToggleCommand = new AsyncCommand(p => ToggleAsync((ContentItem)p!), err);
        DeleteContentCommand = new AsyncCommand(p => DeleteContentAsync((ContentItem)p!), err);
        CreateBackupCommand = new AsyncCommand(_ => CreateBackupAsync(), err);
        RestoreCommand = new AsyncCommand(p => RestoreAsync((BackupInfo)p!), err);
        DeleteBackupCommand = new RelayCommand(p => DeleteBackup((BackupInfo)p!));
        OpenBackupFolderCommand = new RelayCommand(_ => FileUtil.OpenFolder(_backupSvc.Dir));
        StoreCommand = new RelayCommand(p => InstallationService.OpenStore(((InstallInfo)p!).Channel));
        UninstallCommand = new AsyncCommand(p => UninstallAsync((InstallInfo)p!), err);
        OpenDataFolderCommand = new RelayCommand(p => FileUtil.OpenFolder(((InstallInfo)p!).DataPath));
        OpenUrlCommand = new RelayCommand(p => OpenUrl((string)p!));
        SelectProfileCommand = new RelayCommand(p => { SelectedProfile = (Profile)p!; _ = SafeAsync(RefreshContentAndBackupsAsync); });
        DeleteProfileCommand = new RelayCommand(p => DeleteProfile((Profile)p!));
        SetNewChannelCommand = new RelayCommand(p => { _newChannel = Enum.Parse<Channel>((string)p!); OnChanged(nameof(NewChannelName)); });
        CreateProfileCommand = new RelayCommand(_ => CreateProfile());
        SetLanguageCommand = new RelayCommand(p => { Settings.Language = (string)p!; L.SetLanguage(Settings.Language); _settingsSvc.Save(Settings); ReloadViews(); });
        SetThemeCommand = new RelayCommand(p => { Settings.Theme = (string)p!; ThemeManager.Apply(Settings); _settingsSvc.Save(Settings); });
        ApplyAccentCommand = new RelayCommand(_ => { ThemeManager.Apply(Settings); _settingsSvc.Save(Settings); });
        PickBackgroundCommand = new RelayCommand(_ => PickBackground());
        ClearBackgroundCommand = new RelayCommand(_ => { Settings.BackgroundImage = null; BackgroundImage = null; _settingsSvc.Save(Settings); });
        PickBackupDirCommand = new RelayCommand(_ => { var d = new OpenFolderDialog(); if (d.ShowDialog() == true) { Settings.BackupDir = d.FolderName; OnChanged(nameof(Settings)); _settingsSvc.Save(Settings); _ = SafeAsync(RefreshContentAndBackupsAsync); } });
        SaveSettingsCommand = new RelayCommand(_ => { _settingsSvc.Save(Settings); ThemeManager.Apply(Settings); Notify(L["msg_saved"]); _ = SafeAsync(RefreshContentAndBackupsAsync); });
        CheckUpdatesCommand = new AsyncCommand(_ => CheckUpdatesAsync(), err);
        DownloadUpdateCommand = new AsyncCommand(_ => DownloadUpdateAsync(), err);
        CleanTempCommand = new AsyncCommand(_ => CleanTempAsync(), err);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel());
        ReloadNewsCommand = new AsyncCommand(_ => LoadNewsAsync(), err);
    }

    // ---- Inicialización / refresco ----
    public async Task InitializeAsync()
    {
        foreach (var p in _profileSvc.Load()) Profiles.Add(p);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == Settings.SelectedProfileId) ?? Profiles.First();
        await SafeAsync(RefreshAllAsync);
        _ = SafeAsync(LoadNewsAsync);
    }

    async Task SafeAsync(Func<Task> f) { try { await f(); } catch (Exception ex) { Log.Error("Operación", ex); Notify(ex.Message, true); } }

    public async Task RefreshAllAsync()
    {
        var list = await _installSvc.DetectAsync();
        Installs.Clear(); foreach (var i in list) Installs.Add(i);
        OnChanged(nameof(CurrentInstall)); OnChanged(nameof(SelectedVersionText)); OnChanged(nameof(StatusText));
        await RefreshContentAndBackupsAsync();
    }

    async Task RefreshContentAndBackupsAsync()
    {
        var inst = CurrentInstall;
        var items = inst == null ? new List<ContentItem>() : await Task.Run(() => _content.Scan(inst));
        ContentItems.Clear(); foreach (var i in items) ContentItems.Add(i);
        Backups.Clear(); foreach (var b in _backupSvc.List()) Backups.Add(b);
        var data = Installs.Sum(i => i.DataSize); var bk = Backups.Sum(b => b.Size);
        var own = await Task.Run(() => FileUtil.DirSize(AppPaths.Root));
        StorageText = $"Minecraft: {FileUtil.FormatBytes(data)}   ·   {L["nav_backups"]}: {FileUtil.FormatBytes(bk)} ({Backups.Count})   ·   Launcher: {FileUtil.FormatBytes(own)}";
        ContentView.Refresh();
    }

    void ReloadViews() { foreach (var c in ContentItems) c.RefreshText(); ContentView.Refresh(); OnChanged(nameof(SelectedVersionText)); OnChanged(nameof(StatusText)); }

    async Task LoadNewsAsync()
    {
        try
        {
            var all = await _newsSvc.GetAsync(Settings.NewsUrl);
            Releases.Clear(); foreach (var n in all) Releases.Add(n);
            HomeNews.Clear(); foreach (var n in all.Take(5)) HomeNews.Add(n);
            LatestRelease = all.FirstOrDefault(n => !n.IsPreview) ?? all.FirstOrDefault();
        }
        catch (Exception ex) { Log.Error("Noticias", ex); Notify(L["msg_news_fail"], true); }
    }

    // ---- Jugar ----
    void Play()
    {
        var inst = CurrentInstall;
        if (inst == null || !inst.Installed) { Notify(L["msg_not_installed"], true); return; }
        _installSvc.Launch(inst);
        if (SelectedProfile != null) { SelectedProfile.LastPlayed = DateTime.Now; _profileSvc.Save(Profiles); }
        Notify(L["msg_launching"]);
    }

    // ---- Contenido ----
    async Task ImportAsync()
    {
        var inst = CurrentInstall;
        if (inst == null || !inst.Installed) { Notify(L["msg_not_installed"], true); return; }
        var dlg = new OpenFileDialog { Multiselect = true, Filter = "Bedrock (*.mcpack;*.mcaddon;*.mcworld;*.mctemplate;*.zip)|*.mcpack;*.mcaddon;*.mcworld;*.mctemplate;*.zip" };
        if (dlg.ShowDialog() != true) return;
        await RunBusy("busy_import", async (ct, prog) =>
        {
            if (Settings.AutoBackup && Directory.Exists(inst.DataPath) && FileUtil.DirSize(inst.DataPath) > 0)
                await _backupSvc.CreateAsync(inst, "pre-import", prog, ct);
            var n = 0;
            foreach (var f in dlg.FileNames)
            {
                var r = await _content.ImportAsync(f, inst, ct);
                foreach (var ok in r.Imported) Notify(L.Get("msg_imported", ok));
                foreach (var e in r.Errors) Notify(e, true);
                prog.Report(++n * 100.0 / dlg.FileNames.Length);
            }
            await RefreshContentAndBackupsAsync();
        });
    }

    async Task ToggleAsync(ContentItem item)
    {
        var inst = CurrentInstall; if (inst == null) return;
        if (FileUtil.IsGameRunning()) { Notify(L["msg_game_running"], true); return; }
        await Task.Run(() => _content.SetEnabled(item, inst, !item.Enabled));
        await RefreshContentAndBackupsAsync();
    }

    async Task DeleteContentAsync(ContentItem item)
    {
        if (FileUtil.IsGameRunning()) { Notify(L["msg_game_running"], true); return; }
        if (MessageBox.Show(L.Get("msg_confirm_delete", item.Name), "Bedrock Ultimate Launcher", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await Task.Run(() => _content.Delete(item));
        Notify(L["msg_deleted"]);
        await RefreshContentAndBackupsAsync();
    }

    // ---- Copias ----
    async Task CreateBackupAsync()
    {
        var inst = CurrentInstall;
        if (inst == null || !Directory.Exists(inst.DataPath)) { Notify(L["msg_not_installed"], true); return; }
        await RunBusy("busy_backup", async (ct, prog) =>
        {
            var b = await _backupSvc.CreateAsync(inst, SelectedProfile?.Name ?? "backup", prog, ct);
            Notify(L.Get("msg_backup_done", b.Name));
            await RefreshContentAndBackupsAsync();
        });
    }

    async Task RestoreAsync(BackupInfo b)
    {
        var inst = Installs.FirstOrDefault(i => i.Channel == b.Channel);
        if (inst == null || !inst.Installed) { Notify(L["msg_not_installed"], true); return; }
        if (FileUtil.IsGameRunning()) { Notify(L["msg_game_running"], true); return; }
        if (MessageBox.Show(L.Get("msg_confirm_restore", b.Name), "Bedrock Ultimate Launcher", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await RunBusy("busy_restore", async (ct, prog) =>
        {
            if (Settings.AutoBackup && Directory.Exists(inst.DataPath)) await _backupSvc.CreateAsync(inst, "pre-restore", null, ct);
            await _backupSvc.RestoreAsync(b, inst, prog, ct);
            Notify(L["msg_restored"]);
            await RefreshAllAsync();
        });
    }

    void DeleteBackup(BackupInfo b)
    {
        if (MessageBox.Show(L.Get("msg_confirm_delete", b.Name), "Bedrock Ultimate Launcher", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _backupSvc.Delete(b); Backups.Remove(b); Notify(L["msg_deleted"]);
    }

    // ---- Versiones ----
    async Task UninstallAsync(InstallInfo inst)
    {
        if (!inst.Installed) return;
        if (MessageBox.Show(L.Get("msg_confirm_uninstall", inst.ChannelName), "Bedrock Ultimate Launcher", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunBusy("busy_uninstall", async (ct, prog) =>
        {
            if (Directory.Exists(inst.DataPath)) await _backupSvc.CreateAsync(inst, "pre-uninstall", prog, ct); // si falla la copia NO se desinstala
            await _installSvc.UninstallAsync(inst);
            await RefreshAllAsync();
        });
    }

    static void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == "ms-windows-store"))
            Process.Start(new ProcessStartInfo(u.ToString()) { UseShellExecute = true });
    }

    // ---- Perfiles ----
    void CreateProfile()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName)) { Notify(L["msg_profile_exists"], true); return; }
        var p = new Profile { Name = NewProfileName.Trim(), Channel = _newChannel };
        Profiles.Add(p); _profileSvc.Save(Profiles); NewProfileName = "";
    }
    void DeleteProfile(Profile p)
    {
        if (Profiles.Count <= 1) return;
        if (MessageBox.Show(L.Get("msg_confirm_delete", p.Name), "Bedrock Ultimate Launcher", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        Profiles.Remove(p); _profileSvc.Save(Profiles);
        if (SelectedProfile == p) { SelectedProfile = Profiles.First(); _ = SafeAsync(RefreshContentAndBackupsAsync); }
    }

    // ---- Ajustes ----
    void PickBackground()
    {
        var d = new OpenFileDialog { Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp" };
        if (d.ShowDialog() != true) return;
        Settings.BackgroundImage = d.FileName; BackgroundImage = d.FileName; _settingsSvc.Save(Settings);
    }

    async Task CheckUpdatesAsync()
    {
        if (string.IsNullOrWhiteSpace(Settings.UpdateUrl)) { UpdateText = L["msg_no_url"]; Notify(L["msg_no_url"], true); return; }
        _pendingUpdate = await _updateSvc.CheckAsync(Settings.UpdateUrl, CancellationToken.None);
        UpdateText = _pendingUpdate == null ? L["msg_no_updates"] : L.Get("msg_update_avail", _pendingUpdate.Version);
        OnChanged(nameof(UpdateAvailable)); Notify(UpdateText);
    }

    async Task DownloadUpdateAsync()
    {
        var u = _pendingUpdate?.DownloadUrl; if (u == null) return;
        await RunBusy("busy_download", async (ct, prog) =>
        {
            var file = await _updateSvc.DownloadAsync(u, prog, ct);
            Notify(L["msg_downloaded"]);
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
        });
    }

    async Task CleanTempAsync()
    {
        await Task.Run(() => { foreach (var d in new[] { AppPaths.Temp, AppPaths.Downloads }) if (Directory.Exists(d)) foreach (var e in Directory.EnumerateFileSystemEntries(d)) { try { if (Directory.Exists(e)) Directory.Delete(e, true); else File.Delete(e); } catch { } } });
        Notify(L["msg_cleaned"]); await RefreshContentAndBackupsAsync();
    }

    // ---- Infraestructura: operaciones largas con progreso y cancelación ----
    async Task RunBusy(string textKey, Func<CancellationToken, IProgress<double>, Task> work)
    {
        if (IsBusy) return;
        IsBusy = true; BusyText = L[textKey]; Progress = 0; _cts = new CancellationTokenSource();
        var prog = new Progress<double>(v => Progress = v);
        try { await work(_cts.Token, prog); }
        catch (OperationCanceledException) { Notify(L["msg_cancelled"]); }
        catch (Exception ex) { Log.Error("Operación larga", ex); Notify(ex.Message, true); }
        finally { IsBusy = false; _cts?.Dispose(); _cts = null; }
    }

    public async void Notify(string text, bool error = false)
    {
        var n = new Notification { Text = text, IsError = error };
        Application.Current.Dispatcher.Invoke(() => Notifications.Add(n));
        await Task.Delay(error ? 7000 : 4000);
        Application.Current.Dispatcher.Invoke(() => Notifications.Remove(n));
    }
}
