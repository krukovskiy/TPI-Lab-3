using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using EdfBrowser.App.Infrastructure;
using EdfBrowser.App.Models;

namespace EdfBrowser.App.ViewModels;

/// <summary>
/// ViewModel managing the waveform viewport state: visible time window,
/// zoom history, crosshairs, and display options.
/// </summary>
public sealed partial class SignalViewerViewModel : ObservableObject
{
    private const int MaxZoomHistorySize = 16;

    /// <summary>Left edge time of the viewport in 100-ns ticks.</summary>
    [ObservableProperty]
    private long _viewTimeTicks;

    /// <summary>Visible duration in 100-ns ticks.</summary>
    [ObservableProperty]
    private long _pageTimeTicks;

    /// <summary>Viewport width in pixels.</summary>
    [ObservableProperty]
    private int _viewportWidth = 1280;

    /// <summary>Viewport height in pixels.</summary>
    [ObservableProperty]
    private int _viewportHeight = 800;

    /// <summary>Whether to draw lines between samples (vs step mode).</summary>
    [ObservableProperty]
    private bool _linearInterpolation = true;

    /// <summary>Whether to draw baseline indicators.</summary>
    [ObservableProperty]
    private bool _showBaselines = true;

    /// <summary>Whether to show the time grid.</summary>
    [ObservableProperty]
    private bool _showGrid = true;

    /// <summary>Whether to show annotation markers on the waveform.</summary>
    [ObservableProperty]
    private bool _showAnnotations = true;

    // --- Zoom history (circular buffer, max 16 entries) ---

    private readonly ZoomHistoryEntry[] _zoomHistory = new ZoomHistoryEntry[MaxZoomHistorySize];
    private int _zoomHistoryCount;
    private int _zoomHistoryIndex;

    // --- Crosshairs ---

    /// <summary>First crosshair cursor state.</summary>
    public CrosshairState Crosshair1 { get; } = new();

    /// <summary>Second crosshair cursor state.</summary>
    public CrosshairState Crosshair2 { get; } = new();

    // --- File reference ---

    /// <summary>Total file duration in 100-ns ticks.</summary>
    public long FileDurationTicks { get; set; }

    // --- Event to trigger re-render ---

    /// <summary>Raised when the view state changes and the waveform needs to be redrawn.</summary>
    public event Action? ViewChanged;

    // --- Property change handlers ---

    partial void OnViewTimeTicksChanged(long value) => NotifyViewChanged();
    partial void OnPageTimeTicksChanged(long value) => NotifyViewChanged();
    partial void OnLinearInterpolationChanged(bool value) => NotifyViewChanged();
    partial void OnShowBaselinesChanged(bool value) => NotifyViewChanged();
    partial void OnShowGridChanged(bool value) => NotifyViewChanged();
    partial void OnShowAnnotationsChanged(bool value) => NotifyViewChanged();

    // --- Navigation methods ---

    /// <summary>
    /// Scrolls the view one page to the left.
    /// </summary>
    public void PageLeft()
    {
        long newTime = ViewTimeTicks - PageTimeTicks;
        ViewTimeTicks = Math.Max(0, newTime);
        SendViewTimeMessage();
    }

    /// <summary>
    /// Scrolls the view one page to the right.
    /// </summary>
    public void PageRight()
    {
        long newTime = ViewTimeTicks + PageTimeTicks;
        long maxTime = FileDurationTicks - PageTimeTicks;
        ViewTimeTicks = Math.Max(0, Math.Min(newTime, maxTime));
        SendViewTimeMessage();
    }

    /// <summary>
    /// Zooms in by halving the page time, centered on the current view center.
    /// </summary>
    public void ZoomIn()
    {
        PushZoomHistory();

        long center = ViewTimeTicks + PageTimeTicks / 2;
        long newPageTime = PageTimeTicks / 2;
        if (newPageTime < 10_000) // minimum 1ms
            newPageTime = 10_000;

        PageTimeTicks = newPageTime;
        ViewTimeTicks = Math.Max(0, center - newPageTime / 2);

        ClampViewTime();
        SendZoomMessage();
    }

    /// <summary>
    /// Zooms out by doubling the page time, centered on the current view center.
    /// </summary>
    public void ZoomOut()
    {
        PushZoomHistory();

        long center = ViewTimeTicks + PageTimeTicks / 2;
        long newPageTime = PageTimeTicks * 2;
        if (newPageTime > FileDurationTicks)
            newPageTime = FileDurationTicks;

        PageTimeTicks = newPageTime;
        ViewTimeTicks = Math.Max(0, center - newPageTime / 2);

        ClampViewTime();
        SendZoomMessage();
    }

    /// <summary>
    /// Returns to the previous zoom level from history.
    /// </summary>
    public void ZoomBack()
    {
        if (_zoomHistoryCount == 0) return;

        _zoomHistoryIndex = (_zoomHistoryIndex - 1 + MaxZoomHistorySize) % MaxZoomHistorySize;
        _zoomHistoryCount--;

        var entry = _zoomHistory[_zoomHistoryIndex];
        ViewTimeTicks = entry.ViewTimeTicks;
        PageTimeTicks = entry.PageTimeTicks;

        SendZoomMessage();
    }

    /// <summary>
    /// Navigates to the beginning of the file.
    /// </summary>
    public void GoToStart()
    {
        ViewTimeTicks = 0;
        SendViewTimeMessage();
    }

    /// <summary>
    /// Navigates to the end of the file.
    /// </summary>
    public void GoToEnd()
    {
        ViewTimeTicks = Math.Max(0, FileDurationTicks - PageTimeTicks);
        SendViewTimeMessage();
    }

    /// <summary>
    /// Adjusts the zoom to show the entire file duration.
    /// </summary>
    public void FitToScreen(long fileDuration)
    {
        PushZoomHistory();

        FileDurationTicks = fileDuration;
        ViewTimeTicks = 0;
        PageTimeTicks = fileDuration;

        SendZoomMessage();
    }

    /// <summary>
    /// Sets the page time to the specified number of seconds.
    /// </summary>
    public void SetPageTime(double seconds)
    {
        PushZoomHistory();

        long ticks = (long)(seconds * 10_000_000L);
        if (ticks < 10_000) ticks = 10_000;
        if (ticks > FileDurationTicks) ticks = FileDurationTicks;

        long center = ViewTimeTicks + PageTimeTicks / 2;
        PageTimeTicks = ticks;
        ViewTimeTicks = Math.Max(0, center - ticks / 2);

        ClampViewTime();
        SendZoomMessage();
    }

    /// <summary>
    /// Navigates to a specific time position (centering the view).
    /// </summary>
    public void NavigateToTime(long ticks)
    {
        ViewTimeTicks = Math.Max(0, Math.Min(ticks - PageTimeTicks / 2, FileDurationTicks - PageTimeTicks));
        if (ViewTimeTicks < 0) ViewTimeTicks = 0;
        SendViewTimeMessage();
    }

    // --- Private helpers ---

    private void PushZoomHistory()
    {
        _zoomHistory[_zoomHistoryIndex] = new ZoomHistoryEntry(ViewTimeTicks, PageTimeTicks);
        _zoomHistoryIndex = (_zoomHistoryIndex + 1) % MaxZoomHistorySize;
        if (_zoomHistoryCount < MaxZoomHistorySize)
            _zoomHistoryCount++;
    }

    private void ClampViewTime()
    {
        long maxView = FileDurationTicks - PageTimeTicks;
        if (maxView < 0) maxView = 0;
        if (ViewTimeTicks > maxView)
            ViewTimeTicks = maxView;
        if (ViewTimeTicks < 0)
            ViewTimeTicks = 0;
    }

    private void SendViewTimeMessage()
    {
        WeakReferenceMessenger.Default.Send(new ViewTimeChangedMessage(ViewTimeTicks, PageTimeTicks));
    }

    private void SendZoomMessage()
    {
        WeakReferenceMessenger.Default.Send(new ZoomChangedMessage(ViewTimeTicks, PageTimeTicks, _zoomHistoryIndex));
    }

    private void NotifyViewChanged()
    {
        ViewChanged?.Invoke();
        WeakReferenceMessenger.Default.Send(new InvalidateViewMessage());
    }
}
