namespace EdfBrowser.App.Infrastructure;

// -----------------------------------------------------------------------
// File management messages
// -----------------------------------------------------------------------

/// <summary>Sent when an EDF file has been successfully opened.</summary>
/// <param name="FileIndex">The index slot assigned to the file.</param>
/// <param name="FilePath">The full path to the opened file.</param>
public sealed record FileOpenedMessage(int FileIndex, string FilePath);

/// <summary>Sent when a single EDF file has been closed.</summary>
/// <param name="FileIndex">The index of the closed file.</param>
public sealed record FileClosedMessage(int FileIndex);

/// <summary>Sent when all open EDF files have been closed at once.</summary>
public sealed record AllFilesClosedMessage;

// -----------------------------------------------------------------------
// View state messages
// -----------------------------------------------------------------------

/// <summary>Sent when the visible time window changes (scrolling).</summary>
/// <param name="ViewTimeTicks">Start of the view window in 100-ns ticks.</param>
/// <param name="PageTimeTicks">Duration of the view window in 100-ns ticks.</param>
public sealed record ViewTimeChangedMessage(long ViewTimeTicks, long PageTimeTicks);

/// <summary>Sent when a zoom operation changes the view window and/or sensitivities.</summary>
/// <param name="ViewTimeTicks">Start of the view window in 100-ns ticks.</param>
/// <param name="PageTimeTicks">Duration of the view window in 100-ns ticks.</param>
/// <param name="ZoomHistoryIndex">Current index in the zoom history stack.</param>
public sealed record ZoomChangedMessage(long ViewTimeTicks, long PageTimeTicks, int ZoomHistoryIndex);

// -----------------------------------------------------------------------
// Signal messages
// -----------------------------------------------------------------------

/// <summary>Sent when a signal trace has been added to the display.</summary>
/// <param name="FileIndex">The file index the signal belongs to.</param>
/// <param name="SignalIndex">The signal index within the file.</param>
public sealed record SignalAddedMessage(int FileIndex, int SignalIndex);

/// <summary>Sent when a signal trace has been removed from the display.</summary>
/// <param name="TraceIndex">The display trace index that was removed.</param>
public sealed record SignalRemovedMessage(int TraceIndex);

/// <summary>Sent when the set of selected signal traces has changed.</summary>
/// <param name="SelectedTraceIndices">The currently selected display trace indices.</param>
public sealed record SignalSelectionChangedMessage(IReadOnlyList<int> SelectedTraceIndices);

// -----------------------------------------------------------------------
// Filter messages
// -----------------------------------------------------------------------

/// <summary>Sent when a filter configuration has changed for a signal trace.</summary>
/// <param name="TraceIndex">The display trace index whose filter changed.</param>
public sealed record FilterChangedMessage(int TraceIndex);

// -----------------------------------------------------------------------
// Annotation messages
// -----------------------------------------------------------------------

/// <summary>Sent when a new annotation has been created.</summary>
/// <param name="FileIndex">The file index the annotation belongs to.</param>
/// <param name="OnsetTicks">Onset time of the annotation in 100-ns ticks.</param>
/// <param name="DurationTicks">Duration of the annotation in 100-ns ticks, or null if unspecified.</param>
/// <param name="Description">Text description of the annotation.</param>
public sealed record AnnotationCreatedMessage(int FileIndex, long OnsetTicks, long? DurationTicks, string Description);

/// <summary>Sent when an annotation has been selected in the annotation list.</summary>
/// <param name="FileIndex">The file index the annotation belongs to.</param>
/// <param name="AnnotationIndex">The index of the selected annotation.</param>
public sealed record AnnotationSelectedMessage(int FileIndex, int AnnotationIndex);

/// <summary>Sent when an annotation has been deleted.</summary>
/// <param name="FileIndex">The file index the annotation belongs to.</param>
/// <param name="AnnotationIndex">The index of the deleted annotation.</param>
public sealed record AnnotationDeletedMessage(int FileIndex, int AnnotationIndex);

// -----------------------------------------------------------------------
// Crosshair messages
// -----------------------------------------------------------------------

/// <summary>Sent when a crosshair position has changed.</summary>
/// <param name="CrosshairIndex">The crosshair index (0 or 1).</param>
/// <param name="XPixel">The X pixel coordinate of the crosshair.</param>
/// <param name="YPixel">The Y pixel coordinate of the crosshair.</param>
public sealed record CrosshairMovedMessage(int CrosshairIndex, double XPixel, double YPixel);

// -----------------------------------------------------------------------
// Rendering messages
// -----------------------------------------------------------------------

/// <summary>Sent to request a full redraw of the signal view.</summary>
public sealed record InvalidateViewMessage;

/// <summary>Sent when a render pass has completed.</summary>
public sealed record RenderCompleteMessage;
