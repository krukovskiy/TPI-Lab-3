using EdfBrowser.App.Models;
using EdfBrowser.Dsp.FilterPipeline;
using EdfLib.Core;
using SkiaSharp;

namespace EdfBrowser.App.Rendering;

/// <summary>
/// Describes a signal to be rendered, including its visual properties and data source.
/// </summary>
public sealed class SignalRenderInfo
{
    public int FileIndex { get; set; }
    public int SignalIndex { get; set; }
    public string Label { get; set; } = string.Empty;
    public SKColor Color { get; set; } = SKColors.LimeGreen;
    public double SampleRate { get; set; }
    public int Baseline { get; set; }
    public int ScreenOffset { get; set; }
    public double Sensitivity { get; set; } = 1.0;
    public int Polarity { get; set; } = 1;
    public double BitValue { get; set; }
    public double Offset { get; set; }
    public bool Visible { get; set; } = true;
    public FilterChain? FilterChain { get; set; }
}

/// <summary>
/// Options controlling the visual appearance of the rendered waveform display.
/// </summary>
public sealed class RenderOptions
{
    public bool ShowGrid { get; set; } = true;
    public bool ShowRulers { get; set; } = true;
    public bool ShowLabels { get; set; } = true;
    public bool ShowCrosshair { get; set; }
    public bool ShowAnnotations { get; set; } = true;
    public bool AntiAlias { get; set; } = true;
    public float LineWidth { get; set; } = 1.0f;
    public int CrosshairX { get; set; }
    public int CrosshairY { get; set; }
    public double TimeRulerIntervalSec { get; set; } = 1.0;
    public double AmplitudeGridIntervalUv { get; set; } = 100.0;
}

/// <summary>
/// Theme colors for rendering.
/// </summary>
public sealed class RenderTheme
{
    public SKColor BackgroundColor { get; set; } = new(0xFF1E1E1E);
    public SKColor GridColor { get; set; } = new(0xFF3A3A3A);
    public SKColor RulerColor { get; set; } = new(0xFFAAAAAA);
    public SKColor RulerTextColor { get; set; } = new(0xFFCCCCCC);
    public SKColor LabelColor { get; set; } = new(0xFFDDDDDD);
    public SKColor CrosshairColor { get; set; } = new(0x80FFFFFF);
    public SKColor AnnotationColor { get; set; } = new(0xFFFF8800);
    public SKColor AnnotationTextColor { get; set; } = new(0xFFFFCC00);
    public float RulerFontSize { get; set; } = 11f;
    public float LabelFontSize { get; set; } = 12f;
    public float AnnotationFontSize { get; set; } = 10f;
}

/// <summary>
/// Full two-stage rendering pipeline using SkiaSharp.
/// Stage 1: Multi-threaded sample processing to build line segments.
/// Stage 2: Draw line segments and overlays to an SKCanvas.
/// </summary>
public sealed class WaveformRenderer : IDisposable
{
    private readonly int _threadCount;
    private GraphicsBuffer _graphicsBuffer;
    private bool _dirty = true;
    private bool _disposed;

    public WaveformRenderer()
    {
        _threadCount = Math.Max(1, Environment.ProcessorCount);
        _graphicsBuffer = new GraphicsBuffer(_threadCount, 1_000_000 / _threadCount);
    }

    /// <summary>
    /// Stage 1: Multi-threaded sample processing. Reads samples from EdfReader instances,
    /// applies filter chains, converts to pixel coordinates using CoordinateTransform,
    /// handles min/max decimation when zoomed out, and adds line segments to GraphicsBuffer.
    /// </summary>
    /// <param name="signals">Signal descriptions with rendering parameters.</param>
    /// <param name="files">Open EDF file models keyed by file index.</param>
    /// <param name="viewportWidth">Width of the viewport in pixels.</param>
    /// <param name="viewportHeight">Height of the viewport in pixels.</param>
    /// <param name="viewStartTimeSec">Start time of the visible window in seconds.</param>
    /// <param name="pageTimeSec">Duration of the visible window in seconds.</param>
    public void RenderStage1(
        IReadOnlyList<SignalRenderInfo> signals,
        IReadOnlyDictionary<int, EdfFileModel> files,
        int viewportWidth,
        int viewportHeight,
        double viewStartTimeSec,
        double pageTimeSec)
    {
        ThrowIfDisposed();

        if (signals.Count == 0 || viewportWidth <= 0 || viewportHeight <= 0 || pageTimeSec <= 0)
            return;

        int maxSegmentsPerThread = Math.Max(viewportWidth * signals.Count * 4 / _threadCount, 65536);
        _graphicsBuffer = new GraphicsBuffer(_threadCount, maxSegmentsPerThread);

        var visibleSignals = new List<(int Index, SignalRenderInfo Signal)>();
        for (int i = 0; i < signals.Count; i++)
        {
            if (signals[i].Visible)
                visibleSignals.Add((i, signals[i]));
        }

        if (visibleSignals.Count == 0)
            return;

        // Distribute signals across threads
        var threadAssignments = new List<(int Index, SignalRenderInfo Signal)>[_threadCount];
        for (int t = 0; t < _threadCount; t++)
            threadAssignments[t] = new List<(int, SignalRenderInfo)>();

        for (int i = 0; i < visibleSignals.Count; i++)
            threadAssignments[i % _threadCount].Add(visibleSignals[i]);

        Parallel.For(0, _threadCount, threadIdx =>
        {
            var assignments = threadAssignments[threadIdx];
            foreach (var (signalIdx, signal) in assignments)
            {
                if (!files.TryGetValue(signal.FileIndex, out var fileModel))
                    continue;

                ProcessSignal(threadIdx, signalIdx, signal, fileModel,
                    viewportWidth, viewStartTimeSec, pageTimeSec);
            }
        });

        _dirty = false;
    }

    /// <summary>
    /// Stage 2: Draw to SKCanvas. Renders background, grid, rulers, waveforms
    /// (line segments grouped by signal color), annotation markers, crosshairs,
    /// and signal labels.
    /// </summary>
    public void RenderStage2(
        SKCanvas canvas,
        int width,
        int height,
        IReadOnlyList<SignalRenderInfo> signals,
        RenderTheme theme,
        RenderOptions options,
        IReadOnlyList<EdfAnnotation>? annotations = null,
        double viewStartTimeSec = 0,
        double pageTimeSec = 10)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(options);

        // Draw background
        canvas.Clear(theme.BackgroundColor);

        if (width <= 0 || height <= 0)
            return;

        // Draw grid
        if (options.ShowGrid)
            DrawGrid(canvas, width, height, theme, options, viewStartTimeSec, pageTimeSec);

        // Draw rulers
        if (options.ShowRulers)
            DrawRulers(canvas, width, height, theme, options, viewStartTimeSec, pageTimeSec);

        // Draw waveforms (line segments grouped by signal color)
        DrawWaveforms(canvas, signals, options);

        // Draw annotations
        if (options.ShowAnnotations && annotations is { Count: > 0 })
            DrawAnnotations(canvas, width, height, annotations, theme, viewStartTimeSec, pageTimeSec);

        // Draw crosshair
        if (options.ShowCrosshair)
            DrawCrosshair(canvas, width, height, theme, options);

        // Draw signal labels
        if (options.ShowLabels)
            DrawLabels(canvas, signals, theme);
    }

    /// <summary>
    /// Marks the buffer as dirty so Stage 1 will be re-run.
    /// </summary>
    public void Invalidate()
    {
        _dirty = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    // -----------------------------------------------------------------------
    // Stage 1 helpers
    // -----------------------------------------------------------------------

    private void ProcessSignal(
        int threadIndex,
        int signalIndex,
        SignalRenderInfo signal,
        EdfFileModel fileModel,
        int viewportWidth,
        double viewStartTimeSec,
        double pageTimeSec)
    {
        double sampleRate = signal.SampleRate;
        if (sampleRate <= 0) return;

        long viewStartSample = (long)(viewStartTimeSec * sampleRate);
        long viewEndSample = (long)((viewStartTimeSec + pageTimeSec) * sampleRate);
        long totalSamples = fileModel.Reader.GetTotalSamples(signal.SignalIndex);

        if (viewStartSample < 0) viewStartSample = 0;
        if (viewEndSample > totalSamples) viewEndSample = totalSamples;

        long visibleSampleCount = viewEndSample - viewStartSample;
        if (visibleSampleCount <= 0) return;

        var transform = new CoordinateTransform
        {
            SamplePixelRatio = (double)visibleSampleCount / viewportWidth,
            Sensitivity = signal.Sensitivity,
            Baseline = signal.Baseline,
            ScreenOffset = signal.ScreenOffset,
            Polarity = signal.Polarity,
            BitValue = signal.BitValue,
            Offset = signal.Offset,
            ViewportWidth = viewportWidth
        };

        // Seek to the start sample
        fileModel.Reader.Seek(signal.SignalIndex, viewStartSample, SeekOrigin.Begin);

        bool useDecimation = visibleSampleCount > viewportWidth * 2;

        if (useDecimation)
        {
            RenderDecimated(threadIndex, signalIndex, signal, fileModel,
                transform, viewStartSample, visibleSampleCount, viewportWidth);
        }
        else
        {
            RenderDirect(threadIndex, signalIndex, signal, fileModel,
                transform, viewStartSample, visibleSampleCount);
        }
    }

    private void RenderDirect(
        int threadIndex,
        int signalIndex,
        SignalRenderInfo signal,
        EdfFileModel fileModel,
        CoordinateTransform transform,
        long viewStartSample,
        long sampleCount)
    {
        const int chunkSize = 4096;
        double[] buffer = new double[chunkSize];

        float prevX = float.NaN;
        float prevY = float.NaN;
        long sampleOffset = 0;

        while (sampleOffset < sampleCount)
        {
            int toRead = (int)Math.Min(chunkSize, sampleCount - sampleOffset);
            int read = fileModel.Reader.ReadPhysicalSamples(signal.SignalIndex, buffer, toRead);
            if (read == 0) break;

            for (int i = 0; i < read; i++)
            {
                double value = buffer[i];
                if (signal.FilterChain != null)
                    value = signal.FilterChain.Process(value);

                long sampleIdx = viewStartSample + sampleOffset + i;
                float x = transform.SampleToPixelX(sampleIdx, viewStartSample);
                float y = transform.ValueToPixelY(value);

                if (!float.IsNaN(prevX))
                {
                    _graphicsBuffer.AddSegment(threadIndex, prevX, prevY, x, y, signalIndex);
                }

                prevX = x;
                prevY = y;
            }

            sampleOffset += read;
        }
    }

    private void RenderDecimated(
        int threadIndex,
        int signalIndex,
        SignalRenderInfo signal,
        EdfFileModel fileModel,
        CoordinateTransform transform,
        long viewStartSample,
        long visibleSampleCount,
        int viewportWidth)
    {
        // Min/max decimation: for each pixel column, find min and max sample values
        double samplesPerPixel = (double)visibleSampleCount / viewportWidth;
        const int chunkSize = 4096;
        double[] buffer = new double[chunkSize];

        float prevX = float.NaN;
        float prevY = float.NaN;

        for (int px = 0; px < viewportWidth; px++)
        {
            long blockStart = viewStartSample + (long)(px * samplesPerPixel);
            long blockEnd = viewStartSample + (long)((px + 1) * samplesPerPixel);
            if (blockEnd > viewStartSample + visibleSampleCount)
                blockEnd = viewStartSample + visibleSampleCount;

            long blockLength = blockEnd - blockStart;
            if (blockLength <= 0) continue;

            // Seek to block start
            fileModel.Reader.Seek(signal.SignalIndex, blockStart, SeekOrigin.Begin);

            double minVal = double.MaxValue;
            double maxVal = double.MinValue;
            long samplesRead = 0;

            while (samplesRead < blockLength)
            {
                int toRead = (int)Math.Min(chunkSize, blockLength - samplesRead);
                int read = fileModel.Reader.ReadPhysicalSamples(signal.SignalIndex, buffer, toRead);
                if (read == 0) break;

                for (int i = 0; i < read; i++)
                {
                    double value = buffer[i];
                    if (signal.FilterChain != null)
                        value = signal.FilterChain.Process(value);

                    if (value < minVal) minVal = value;
                    if (value > maxVal) maxVal = value;
                }

                samplesRead += read;
            }

            if (minVal > maxVal) continue;

            float x = px;
            float yMin = transform.ValueToPixelY(minVal);
            float yMax = transform.ValueToPixelY(maxVal);

            // Draw line from previous point to min, then min to max within the pixel
            if (!float.IsNaN(prevX))
            {
                _graphicsBuffer.AddSegment(threadIndex, prevX, prevY, x, yMin, signalIndex);
            }

            if (Math.Abs(yMin - yMax) > 0.5f)
            {
                _graphicsBuffer.AddSegment(threadIndex, x, yMin, x, yMax, signalIndex);
            }

            prevX = x;
            prevY = yMax;
        }
    }

    // -----------------------------------------------------------------------
    // Stage 2 helpers
    // -----------------------------------------------------------------------

    private static void DrawGrid(
        SKCanvas canvas, int width, int height,
        RenderTheme theme, RenderOptions options,
        double viewStartTimeSec, double pageTimeSec)
    {
        using var gridPaint = new SKPaint
        {
            Color = theme.GridColor,
            StrokeWidth = 1f,
            IsAntialias = false,
            Style = SKPaintStyle.Stroke
        };

        // Vertical grid lines (time intervals)
        double interval = options.TimeRulerIntervalSec;
        if (interval > 0 && pageTimeSec > 0)
        {
            double firstLine = Math.Ceiling(viewStartTimeSec / interval) * interval;
            for (double t = firstLine; t < viewStartTimeSec + pageTimeSec; t += interval)
            {
                float x = (float)((t - viewStartTimeSec) / pageTimeSec * width);
                canvas.DrawLine(x, 0, x, height, gridPaint);
            }
        }

        // Horizontal grid lines (amplitude intervals) at fixed pixel spacing
        double ampInterval = options.AmplitudeGridIntervalUv;
        if (ampInterval > 0)
        {
            int gridSpacingPx = Math.Max(50, height / 20);
            for (int y = gridSpacingPx; y < height; y += gridSpacingPx)
            {
                canvas.DrawLine(0, y, width, y, gridPaint);
            }
        }
    }

    private static void DrawRulers(
        SKCanvas canvas, int width, int height,
        RenderTheme theme, RenderOptions options,
        double viewStartTimeSec, double pageTimeSec)
    {
        using var rulerPaint = new SKPaint
        {
            Color = theme.RulerColor,
            StrokeWidth = 1f,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        using var textPaint = new SKPaint
        {
            Color = theme.RulerTextColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var font = new SKFont
        {
            Size = theme.RulerFontSize
        };

        // Time ruler along the bottom
        float rulerY = height - 2;
        canvas.DrawLine(0, rulerY, width, rulerY, rulerPaint);

        double interval = options.TimeRulerIntervalSec;
        if (interval > 0 && pageTimeSec > 0)
        {
            double firstTick = Math.Ceiling(viewStartTimeSec / interval) * interval;
            for (double t = firstTick; t < viewStartTimeSec + pageTimeSec; t += interval)
            {
                float x = (float)((t - viewStartTimeSec) / pageTimeSec * width);
                canvas.DrawLine(x, rulerY - 5, x, rulerY, rulerPaint);

                string label = FormatTime(t);
                canvas.DrawText(label, x + 2, rulerY - 7, font, textPaint);
            }
        }
    }

    private void DrawWaveforms(SKCanvas canvas, IReadOnlyList<SignalRenderInfo> signals, RenderOptions options)
    {
        var segmentsBySignal = _graphicsBuffer.GetSegmentsBySignal();

        foreach (var (signalIdx, segments) in segmentsBySignal)
        {
            if (signalIdx < 0 || signalIdx >= signals.Count)
                continue;

            var signal = signals[signalIdx];
            if (!signal.Visible)
                continue;

            using var paint = new SKPaint
            {
                Color = signal.Color,
                StrokeWidth = options.LineWidth,
                IsAntialias = options.AntiAlias,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round
            };

            foreach (var seg in segments)
            {
                canvas.DrawLine(seg.X1, seg.Y1, seg.X2, seg.Y2, paint);
            }
        }
    }

    private static void DrawAnnotations(
        SKCanvas canvas, int width, int height,
        IReadOnlyList<EdfAnnotation> annotations,
        RenderTheme theme,
        double viewStartTimeSec, double pageTimeSec)
    {
        using var markerPaint = new SKPaint
        {
            Color = theme.AnnotationColor,
            StrokeWidth = 1.5f,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        using var textPaint = new SKPaint
        {
            Color = theme.AnnotationTextColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var font = new SKFont
        {
            Size = theme.AnnotationFontSize
        };

        double viewEndTimeSec = viewStartTimeSec + pageTimeSec;

        foreach (var ann in annotations)
        {
            double onsetSec = ann.Onset.TotalSeconds;
            if (onsetSec < viewStartTimeSec || onsetSec > viewEndTimeSec)
                continue;

            float x = (float)((onsetSec - viewStartTimeSec) / pageTimeSec * width);

            // Draw vertical marker line
            canvas.DrawLine(x, 0, x, height, markerPaint);

            // Draw annotation text
            string text = ann.Description;
            if (text.Length > 40)
                text = string.Concat(text.AsSpan(0, 37), "...");

            canvas.DrawText(text, x + 3, 14, font, textPaint);

            // Draw duration region if present
            if (ann.Duration.HasValue && ann.Duration.Value.TotalSeconds > 0)
            {
                double durSec = ann.Duration.Value.TotalSeconds;
                float x2 = (float)((onsetSec + durSec - viewStartTimeSec) / pageTimeSec * width);

                using var regionPaint = new SKPaint
                {
                    Color = theme.AnnotationColor.WithAlpha(30),
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(x, 0, x2 - x, height, regionPaint);
            }
        }
    }

    private static void DrawCrosshair(
        SKCanvas canvas, int width, int height,
        RenderTheme theme, RenderOptions options)
    {
        using var paint = new SKPaint
        {
            Color = theme.CrosshairColor,
            StrokeWidth = 1f,
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0)
        };

        int cx = options.CrosshairX;
        int cy = options.CrosshairY;

        if (cx >= 0 && cx <= width)
            canvas.DrawLine(cx, 0, cx, height, paint);

        if (cy >= 0 && cy <= height)
            canvas.DrawLine(0, cy, width, cy, paint);
    }

    private static void DrawLabels(
        SKCanvas canvas, IReadOnlyList<SignalRenderInfo> signals, RenderTheme theme)
    {
        using var textPaint = new SKPaint
        {
            Color = theme.LabelColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var font = new SKFont
        {
            Size = theme.LabelFontSize
        };

        using var bgPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 160),
            Style = SKPaintStyle.Fill
        };

        foreach (var signal in signals)
        {
            if (!signal.Visible || string.IsNullOrEmpty(signal.Label))
                continue;

            float labelY = signal.Baseline + signal.ScreenOffset;

            // Measure text for background rectangle
            float textWidth = font.MeasureText(signal.Label, textPaint);
            float textHeight = theme.LabelFontSize;

            var bgRect = new SKRect(2, labelY - textHeight - 2, 2 + textWidth + 8, labelY + 4);
            canvas.DrawRect(bgRect, bgPaint);

            textPaint.Color = signal.Color;
            canvas.DrawText(signal.Label, 6, labelY, font, textPaint);
            textPaint.Color = theme.LabelColor;
        }
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 0) seconds = 0;
        int hours = (int)(seconds / 3600);
        int minutes = (int)((seconds % 3600) / 60);
        double secs = seconds % 60;

        if (hours > 0)
            return $"{hours}:{minutes:D2}:{secs:00.0}";
        if (minutes > 0)
            return $"{minutes}:{secs:00.0}";
        return $"{secs:0.000}s";
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
