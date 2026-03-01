namespace EdfBrowser.App.Services;

public interface IClipboardService
{
    void CopyText(string text);
    void CopyAnnotationAsText(string description, long onsetTicks, long? durationTicks);
}

public sealed class ClipboardService : IClipboardService
{
    public void CopyText(string text)
    {
        System.Windows.Clipboard.SetText(text);
    }

    public void CopyAnnotationAsText(string description, long onsetTicks, long? durationTicks)
    {
        var onset = TimeSpan.FromTicks(onsetTicks);
        var onsetStr = onset.ToString(@"hh\:mm\:ss\.fff");
        var durationStr = durationTicks.HasValue
            ? $"\t{(double)durationTicks.Value / 10_000_000.0:F3}s"
            : "";
        CopyText($"{onsetStr}{durationStr}\t{description}");
    }
}
