namespace EdfBrowser.App.Services;

using System.Xml.Linq;
using System.Windows.Media;
using EdfBrowser.App.Models;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
    string SettingsFilePath { get; }
}

public sealed class SettingsService : ISettingsService
{
    public string SettingsFilePath { get; }

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "EDFbrowser");
        Directory.CreateDirectory(dir);
        SettingsFilePath = Path.Combine(dir, "settings.xml");
    }

    public AppSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
            return new AppSettings();

        try
        {
            var doc = XDocument.Load(SettingsFilePath);
            var root = doc.Root;
            if (root == null) return new AppSettings();

            var s = new AppSettings
            {
                LinearInterpolation = GetBool(root, "LinearInterpolation", true),
                ShowBaselines = GetBool(root, "ShowBaselines", true),
                ShowGrid = GetBool(root, "ShowGrid", true),
                AntiAliasing = GetBool(root, "AntiAliasing", true),
                MaxRecentFiles = GetInt(root, "MaxRecentFiles", 20),
                DefaultPageTimeSec = GetDouble(root, "DefaultPageTimeSec", 10.0),
                ShowAnnotationMarkers = GetBool(root, "ShowAnnotationMarkers", true),
                AnnotationFontSize = GetInt(root, "AnnotationFontSize", 10),
                LastOpenPath = GetString(root, "LastOpenPath"),
                LastSavePath = GetString(root, "LastSavePath"),
                LastMontagePath = GetString(root, "LastMontagePath"),
                LastSessionPath = GetString(root, "LastSessionPath"),
                WindowWidth = GetDouble(root, "WindowWidth", 1280),
                WindowHeight = GetDouble(root, "WindowHeight", 800),
                IsMaximized = GetBool(root, "IsMaximized", false),
                RemoteControlEnabled = GetBool(root, "RemoteControlEnabled", false),
                RemoteControlPort = GetInt(root, "RemoteControlPort", 40100),
                PrintDpi = GetInt(root, "PrintDpi", 300),
                PrintGrayscale = GetBool(root, "PrintGrayscale", false),
            };

            var recentEl = root.Element("RecentFiles");
            if (recentEl != null)
            {
                s.RecentFiles = recentEl.Elements("File")
                    .Select(e => e.Value)
                    .ToList();
            }

            var themeEl = root.Element("Theme");
            if (themeEl != null)
            {
                s.Theme = new ColorTheme
                {
                    BackgroundColor = ParseColor(GetString(themeEl, "BackgroundColor"), Colors.White),
                    GridColor = ParseColor(GetString(themeEl, "GridColor"), Color.FromRgb(220, 220, 220)),
                    RulerColor = ParseColor(GetString(themeEl, "RulerColor"), Colors.Black),
                    TextColor = ParseColor(GetString(themeEl, "TextColor"), Colors.Black),
                    Crosshair1Color = ParseColor(GetString(themeEl, "Crosshair1Color"), Colors.Red),
                    Crosshair2Color = ParseColor(GetString(themeEl, "Crosshair2Color"), Colors.Cyan),
                    BaselineColor = ParseColor(GetString(themeEl, "BaselineColor"), Color.FromRgb(128, 128, 128)),
                    AnnotationColor = ParseColor(GetString(themeEl, "AnnotationColor"), Color.FromRgb(0, 128, 0)),
                };
            }

            return s;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var doc = new XDocument(
            new XElement("Settings",
                new XElement("LinearInterpolation", settings.LinearInterpolation),
                new XElement("ShowBaselines", settings.ShowBaselines),
                new XElement("ShowGrid", settings.ShowGrid),
                new XElement("AntiAliasing", settings.AntiAliasing),
                new XElement("MaxRecentFiles", settings.MaxRecentFiles),
                new XElement("DefaultPageTimeSec", settings.DefaultPageTimeSec),
                new XElement("ShowAnnotationMarkers", settings.ShowAnnotationMarkers),
                new XElement("AnnotationFontSize", settings.AnnotationFontSize),
                new XElement("LastOpenPath", settings.LastOpenPath ?? ""),
                new XElement("LastSavePath", settings.LastSavePath ?? ""),
                new XElement("LastMontagePath", settings.LastMontagePath ?? ""),
                new XElement("LastSessionPath", settings.LastSessionPath ?? ""),
                new XElement("WindowWidth", settings.WindowWidth),
                new XElement("WindowHeight", settings.WindowHeight),
                new XElement("IsMaximized", settings.IsMaximized),
                new XElement("RemoteControlEnabled", settings.RemoteControlEnabled),
                new XElement("RemoteControlPort", settings.RemoteControlPort),
                new XElement("PrintDpi", settings.PrintDpi),
                new XElement("PrintGrayscale", settings.PrintGrayscale),
                new XElement("RecentFiles",
                    settings.RecentFiles.Select(f => new XElement("File", f))),
                new XElement("Theme",
                    new XElement("BackgroundColor", FormatColor(settings.Theme.BackgroundColor)),
                    new XElement("GridColor", FormatColor(settings.Theme.GridColor)),
                    new XElement("RulerColor", FormatColor(settings.Theme.RulerColor)),
                    new XElement("TextColor", FormatColor(settings.Theme.TextColor)),
                    new XElement("Crosshair1Color", FormatColor(settings.Theme.Crosshair1Color)),
                    new XElement("Crosshair2Color", FormatColor(settings.Theme.Crosshair2Color)),
                    new XElement("BaselineColor", FormatColor(settings.Theme.BaselineColor)),
                    new XElement("AnnotationColor", FormatColor(settings.Theme.AnnotationColor))
                )
            )
        );

        doc.Save(SettingsFilePath);
    }

    private static bool GetBool(XElement parent, string name, bool defaultValue)
    {
        var el = parent.Element(name);
        return el != null && bool.TryParse(el.Value, out var v) ? v : defaultValue;
    }

    private static int GetInt(XElement parent, string name, int defaultValue)
    {
        var el = parent.Element(name);
        return el != null && int.TryParse(el.Value, out var v) ? v : defaultValue;
    }

    private static double GetDouble(XElement parent, string name, double defaultValue)
    {
        var el = parent.Element(name);
        return el != null && double.TryParse(el.Value, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
    }

    private static string? GetString(XElement parent, string name)
    {
        var el = parent.Element(name);
        return string.IsNullOrEmpty(el?.Value) ? null : el.Value;
    }

    private static Color ParseColor(string? hex, Color defaultColor)
    {
        if (string.IsNullOrEmpty(hex)) return defaultColor;
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return defaultColor; }
    }

    private static string FormatColor(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
