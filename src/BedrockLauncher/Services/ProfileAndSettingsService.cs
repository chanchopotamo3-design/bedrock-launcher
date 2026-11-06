using BedrockLauncher.Core;
namespace BedrockLauncher.Services;

public class SettingsService
{
    public AppSettings Load() { AppPaths.EnsureAll(); return JsonStore.Load(AppPaths.Settings, () => new AppSettings()); }
    public void Save(AppSettings s) => JsonStore.Save(AppPaths.Settings, s);
}

public class ProfileService
{
    public List<Profile> Load()
    {
        var list = JsonStore.Load(AppPaths.Profiles, () => new List<Profile>());
        if (list.Count == 0)
        {
            list.Add(new Profile { Name = "Minecraft Bedrock", Channel = Channel.Release });
            list.Add(new Profile { Name = "Minecraft Preview", Channel = Channel.Preview });
            Save(list);
        }
        return list;
    }
    public void Save(IEnumerable<Profile> p) => JsonStore.Save(AppPaths.Profiles, p.ToList());
}
