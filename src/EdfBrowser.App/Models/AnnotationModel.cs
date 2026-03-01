namespace EdfBrowser.App.Models;

public sealed class AnnotationModel
{
    public int FileIndex { get; set; }
    public int AnnotationIndex { get; set; }
    public long OnsetTicks { get; set; }
    public long? DurationTicks { get; set; }
    public string Description { get; set; } = "";
    public bool IsSelected { get; set; }
    public bool IsVisible { get; set; } = true;

    public string OnsetText => TimeSpan.FromTicks(OnsetTicks).ToString(@"hh\:mm\:ss\.fff");

    public string DurationText => DurationTicks.HasValue
        ? $"{(double)DurationTicks.Value / 10_000_000.0:F3}s"
        : "";
}
