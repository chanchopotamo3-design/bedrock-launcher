using System.IO.Compression;
using BedrockLauncher.Core;
using BedrockLauncher.Services;
using Xunit;
namespace BedrockLauncher.Tests;

public class CoreTests
{
    [Fact] public void PackageVersionToGameVersion() => Assert.Equal("1.21.50.1", GameVersion.FromPackage("1.21.5001.0"));

    [Fact]
    public async Task ImportsMcpackWithCommentsAndTrailingCommas_AndTogglesIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "bul_" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "com.mojang"); Directory.CreateDirectory(data);
        var pack = Path.Combine(root, "test.mcpack");
        using (var z = ZipFile.Open(pack, ZipArchiveMode.Create))
        {
            var e = z.CreateEntry("manifest.json");
            using var w = new StreamWriter(e.Open());
            w.Write("{ // comentario\n \"format_version\":2, \"header\":{\"name\":\"Mi Pack\",\"description\":\"d\",\"uuid\":\"11111111-2222-3333-4444-555555555555\",\"version\":[1,0,0],\"min_engine_version\":[1,20,0],},\"modules\":[{\"type\":\"resources\",\"uuid\":\"aaaaaaaa-2222-3333-4444-555555555555\",\"version\":[1,0,0]},]}");
        }
        var inst = new InstallInfo { Channel = Channel.Release, Installed = true, Version = "1.21.50.1", DataPath = data };
        var svc = new ContentService();
        var res = await svc.ImportAsync(pack, inst, CancellationToken.None);
        Assert.Empty(res.Errors); Assert.Single(res.Imported);
        var items = svc.Scan(inst);
        Assert.Single(items); Assert.Equal(ContentKind.Resource, items[0].Kind); Assert.True(items[0].Enabled);
        svc.SetEnabled(items[0], inst, false);
        Assert.False(Directory.Exists(Path.Combine(data, "development_resource_packs", Path.GetFileName(items[0].Path))));
        Directory.Delete(root, true);
        if (Directory.Exists(Path.Combine(AppPaths.Disabled, "Release"))) Directory.Delete(Path.Combine(AppPaths.Disabled, "Release"), true);
    }

    [Fact]
    public async Task BackupAndRestoreRoundTrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "bul_" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "com.mojang"); Directory.CreateDirectory(Path.Combine(data, "minecraftWorlds", "w1"));
        File.WriteAllText(Path.Combine(data, "minecraftWorlds", "w1", "level.dat"), "x");
        var s = new AppSettings { BackupDir = Path.Combine(root, "bk") };
        var bs = new BackupService(s);
        var inst = new InstallInfo { Channel = Channel.Release, DataPath = data };
        var b = await bs.CreateAsync(inst, "t", null, CancellationToken.None);
        File.Delete(Path.Combine(data, "minecraftWorlds", "w1", "level.dat"));
        await bs.RestoreAsync(b, inst, null, CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(data, "minecraftWorlds", "w1", "level.dat")));
        Directory.Delete(root, true);
    }
}
