using System.Text.Json.Serialization;
namespace BedrockLauncher.Core;

public enum Channel { Release, Preview }
public enum ContentKind { Behavior, Resource, Skin, World, Template }

public class InstallInfo : ObservableObject
{
    public Channel Channel { get; set; }
    public string Family { get; set; } = "";
    public string PackageName { get; set; } = "";
    public bool Installed { get; set; }
    public string? Version { get; set; }
    public string? FullName { get; set; }
    public string? InstallLocation { get; set; }
    public DateTime? InstalledOn { get; set; }
    public string DataPath { get; set; } = "";
    public long DataSize { get; set; }
    public string AppId => $"{Family}!App";
    public string ChannelName => Channel == Channel.Release ? "Minecraft Bedrock" : "Minecraft Preview";
    public string VersionText => Installed ? (Version ?? "?") : "—";
    public string InstalledOnText => InstalledOn?.ToString("yyyy-MM-dd") ?? "—";
    public string StatusText => Installed ? Localizer.Instance["installed"] : Localizer.Instance["not_installed"];
    public string SizeText => FileUtil.FormatBytes(DataSize);
}

public class ContentItem : ObservableObject
{
    string _path = ""; bool _enabled = true; string _warning = "";
    public ContentKind Kind { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Uuid { get; set; } = "";
    public string Version { get; set; } = "";
    public Version? MinEngine { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public bool IsShader { get; set; }
    public string? IconPath { get; set; }
    public string SubFolder { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Path { get => _path; set => Set(ref _path, value); }
    public bool Enabled { get => _enabled; set { if (Set(ref _enabled, value)) { OnChanged(nameof(StateText)); } } }
    public string Warning { get => _warning; set => Set(ref _warning, value); }
    public bool CanToggle => Kind != ContentKind.World;
    public string StateText => Enabled ? Localizer.Instance["on"] : Localizer.Instance["off"];
    public string CategoryKey => Kind switch
    {
        ContentKind.Behavior => "cat_addons",
        ContentKind.Resource => IsShader ? "cat_shaders" : "cat_textures",
        ContentKind.Skin => "cat_skins",
        ContentKind.World => "cat_worlds",
        _ => "cat_templates"
    };
    public string CategoryText => Localizer.Instance[CategoryKey];
    public void RefreshText() { OnChanged(nameof(StateText)); OnChanged(nameof(CategoryText)); OnChanged(nameof(Subtitle)); }
    public string Subtitle => $"{CategoryText}  ·  {(string.IsNullOrEmpty(Version) ? "" : "v" + Version + "  ·  ")}{FileUtil.FormatBytes(SizeBytes)}";
}

public class Profile : ObservableObject
{
    bool _active;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Perfil";
    public Channel Channel { get; set; }
    public string Notes { get; set; } = "";
    public DateTime? LastPlayed { get; set; }
    [JsonIgnore] public bool IsActive { get => _active; set => Set(ref _active, value); }
    [JsonIgnore] public string LastPlayedText => LastPlayed?.ToString("yyyy-MM-dd HH:mm") ?? "—";
}

public class BackupInfo
{
    public string Path { get; set; } = "";
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
    public DateTime Date { get; set; }
    public long Size { get; set; }
    public Channel Channel { get; set; }
    public string DateText => Date.ToString("yyyy-MM-dd HH:mm");
    public string SizeText => FileUtil.FormatBytes(Size);
}

public class NewsItem
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime Date { get; set; }
    public string Version { get; set; } = "";
    public bool IsPreview { get; set; }
    public string DateText => Date.ToString("yyyy-MM-dd");
    public string TypeText => IsPreview ? "Preview/Beta" : "Release";
}

public class AppSettings
{
    public string Language { get; set; } = "es";
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "#3C8527";
    public string? BackgroundImage { get; set; }
    public string BackupDir { get; set; } = "";
    public int MaxBackups { get; set; } = 10;
    public bool AutoBackup { get; set; } = true;
    public bool Animations { get; set; } = true;
    public bool SoftwareRender { get; set; } = false;
    public string? SelectedProfileId { get; set; }
    public string UpdateUrl { get; set; } = "";   // p. ej. https://api.github.com/repos/USUARIO/REPO/releases/latest
    public string NewsUrl { get; set; } = "https://feedback.minecraft.net/api/v2/help_center/en-us/sections/360001186971/articles.json?per_page=30&sort_by=created_at&sort_order=desc";
}
