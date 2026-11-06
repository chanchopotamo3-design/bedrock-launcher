using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;          // ← esta línea la agregas tú
namespace BedrockLauncher.UI;

public class PageVisConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => string.Equals(v as string, p as string) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
public class NotEmptyVisConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => string.IsNullOrEmpty(v as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
public class ImageConverter : IValueConverter
{
    public object? Convert(object? v, Type t, object? p, CultureInfo c)
    {
        var path = v as string;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {   // OnLoad: no deja el archivo bloqueado (permite borrar/mover el pack)
            var bi = new BitmapImage(); bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.UriSource = new Uri(path); bi.EndInit(); bi.Freeze(); return bi;
        }
        catch { return null; }
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public static class ThemeManager
{
    public static void Apply(AppSettings s)
    {
        var app = Application.Current;
        var uri = new Uri(s.Theme == "Light" ? "Themes/Light.xaml" : "Themes/Dark.xaml", UriKind.Relative);
        app.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = uri };
        Color c;
        try { c = (Color)ColorConverter.ConvertFromString(s.Accent); } catch { c = Color.FromRgb(0x3C, 0x85, 0x27); }
        app.Resources["AccentBrush"] = Frozen(c);
        app.Resources["AccentHiBrush"] = Frozen(Mix(c, Colors.White, 0.28));
        app.Resources["AccentLoBrush"] = Frozen(Mix(c, Colors.Black, 0.45));
    }
    static Color Mix(Color a, Color b, double t) => Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}
