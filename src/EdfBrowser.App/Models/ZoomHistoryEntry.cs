namespace EdfBrowser.App.Models;

/// <summary>
/// Represents a single entry in the zoom history stack.
/// </summary>
public readonly struct ZoomHistoryEntry
{
    /// <summary>Start of the view window in 100-ns ticks.</summary>
    public long ViewTimeTicks { get; init; }

    /// <summary>Duration of the view window in 100-ns ticks.</summary>
    public long PageTimeTicks { get; init; }

    public ZoomHistoryEntry(long viewTimeTicks, long pageTimeTicks)
    {
        ViewTimeTicks = viewTimeTicks;
        PageTimeTicks = pageTimeTicks;
    }
}
