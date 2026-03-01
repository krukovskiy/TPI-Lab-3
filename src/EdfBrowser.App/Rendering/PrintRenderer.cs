namespace EdfBrowser.App.Rendering;

using SkiaSharp;
using EdfBrowser.App.Models;

public enum PrintFormat { Png, Jpeg, Bmp, Pdf }

public sealed class PrintRenderer
{
    public void ExportToImage(
        string filePath, PrintFormat format, int width, int height,
        IReadOnlyList<SignalComponent> signals, IReadOnlyList<EdfFileModel> files,
        long viewTimeTicks, long pageTimeTicks, ColorTheme theme, RenderOptions options,
        bool grayscale = false, int dpi = 300)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        if (grayscale)
        {
            theme = MakeGrayscale(theme);
        }

        var renderer = new WaveformRenderer();
        renderer.RenderStage1(signals, files, width, height, viewTimeTicks, pageTimeTicks);
        renderer.RenderStage2(canvas, width, height, signals, theme, options);

        var skFormat = format switch
        {
            PrintFormat.Png => SKEncodedImageFormat.Png,
            PrintFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            PrintFormat.Bmp => SKEncodedImageFormat.Bmp,
            _ => SKEncodedImageFormat.Png,
        };

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(skFormat, 95);
        using var fileStream = File.OpenWrite(filePath);
        data.SaveTo(fileStream);
    }

    public void ExportToPdf(
        string filePath, float pageWidthMm, float pageHeightMm,
        IReadOnlyList<SignalComponent> signals, IReadOnlyList<EdfFileModel> files,
        long viewTimeTicks, long pageTimeTicks, ColorTheme theme, RenderOptions options,
        bool grayscale = false)
    {
        float pageWidthPt = pageWidthMm * 72f / 25.4f;
        float pageHeightPt = pageHeightMm * 72f / 25.4f;

        using var stream = File.OpenWrite(filePath);
        using var document = SKDocument.CreatePdf(stream);

        var pageCanvas = document.BeginPage(pageWidthPt, pageHeightPt);

        if (grayscale)
        {
            theme = MakeGrayscale(theme);
        }

        int pixelWidth = (int)pageWidthPt;
        int pixelHeight = (int)pageHeightPt;

        var renderer = new WaveformRenderer();
        renderer.RenderStage1(signals, files, pixelWidth, pixelHeight, viewTimeTicks, pageTimeTicks);
        renderer.RenderStage2(pageCanvas, pixelWidth, pixelHeight, signals, theme, options);

        document.EndPage();
        document.Close();
    }

    private static ColorTheme MakeGrayscale(ColorTheme theme)
    {
        var gs = new ColorTheme
        {
            BackgroundColor = ToGrayscaleColor(theme.BackgroundColor),
            GridColor = ToGrayscaleColor(theme.GridColor),
            RulerColor = ToGrayscaleColor(theme.RulerColor),
            TextColor = ToGrayscaleColor(theme.TextColor),
            Crosshair1Color = ToGrayscaleColor(theme.Crosshair1Color),
            Crosshair2Color = ToGrayscaleColor(theme.Crosshair2Color),
            BaselineColor = ToGrayscaleColor(theme.BaselineColor),
            AnnotationColor = ToGrayscaleColor(theme.AnnotationColor),
            FloatingRulerColor = ToGrayscaleColor(theme.FloatingRulerColor),
            SignalColors = theme.SignalColors.Select(c => ToGrayscaleColor(c)).ToArray(),
        };
        return gs;
    }

    private static System.Windows.Media.Color ToGrayscaleColor(System.Windows.Media.Color c)
    {
        byte gray = (byte)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
        return System.Windows.Media.Color.FromRgb(gray, gray, gray);
    }
}
