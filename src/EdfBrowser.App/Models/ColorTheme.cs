namespace EdfBrowser.App.Models;

using System.Windows.Media;

public sealed class ColorTheme
{
    public Color BackgroundColor { get; set; } = Colors.White;
    public Color GridColor { get; set; } = Color.FromRgb(220, 220, 220);
    public Color RulerColor { get; set; } = Colors.Black;
    public Color TextColor { get; set; } = Colors.Black;
    public Color Crosshair1Color { get; set; } = Colors.Red;
    public Color Crosshair2Color { get; set; } = Colors.Cyan;
    public Color BaselineColor { get; set; } = Color.FromRgb(128, 128, 128);
    public Color AnnotationColor { get; set; } = Color.FromRgb(0, 128, 0);
    public Color FloatingRulerColor { get; set; } = Colors.Red;

    public Color[] SignalColors { get; set; } =
    [
        Colors.Black, Colors.Green, Colors.Red, Colors.Blue,
        Colors.Cyan, Colors.Magenta, Color.FromRgb(128, 128, 0), Color.FromRgb(128, 0, 128),
        Color.FromRgb(0, 128, 128), Color.FromRgb(128, 0, 0), Color.FromRgb(0, 128, 0), Color.FromRgb(0, 0, 128),
        Color.FromRgb(255, 128, 0), Color.FromRgb(128, 255, 0), Color.FromRgb(0, 255, 128), Color.FromRgb(0, 128, 255)
    ];

    public Color GetSignalColor(int index) => SignalColors[index % SignalColors.Length];

    public static ColorTheme Default => new();

    public static ColorTheme Dark => new()
    {
        BackgroundColor = Color.FromRgb(30, 30, 30),
        GridColor = Color.FromRgb(60, 60, 60),
        RulerColor = Colors.LightGray,
        TextColor = Colors.LightGray,
        SignalColors =
        [
            Colors.LightGreen, Colors.Cyan, Colors.Yellow, Colors.Orange,
            Colors.Pink, Colors.LightBlue, Colors.Lime, Colors.Coral,
            Colors.Aqua, Colors.Salmon, Colors.LightCyan, Colors.Gold,
            Colors.Thistle, Colors.PaleGreen, Colors.SkyBlue, Colors.Khaki
        ]
    };
}
