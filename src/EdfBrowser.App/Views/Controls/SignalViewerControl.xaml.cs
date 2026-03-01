namespace EdfBrowser.App.Views.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using EdfBrowser.App.Rendering;
using EdfBrowser.App.ViewModels;
using EdfBrowser.App.Models;

public partial class SignalViewerControl : UserControl
{
    private WaveformRenderer? _renderer;
    private bool _isDragging;
    private Point _dragStart;

    public SignalViewerControl()
    {
        InitializeComponent();
        _renderer = new WaveformRenderer();

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        MouseWheel += OnMouseWheel;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SignalViewer.ViewChanged += () =>
                SkiaCanvas.InvalidateVisual();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SignalViewer.ViewportWidth = (int)ActualWidth;
            ViewModel.SignalViewer.ViewportHeight = (int)ActualHeight;
        }
        SkiaCanvas.InvalidateVisual();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(SKColors.White);

        if (ViewModel == null || _renderer == null) return;

        var vm = ViewModel;
        if (vm.SignalTraces.Count == 0 || vm.OpenFiles.Count == 0)
        {
            using var paint = new SKPaint
            {
                Color = SKColors.Gray,
                TextSize = 16,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
            };
            canvas.DrawText("Open an EDF/BDF file and add signals to view",
                info.Width / 2f, info.Height / 2f, paint);
            return;
        }

        // Build signal components from trace ViewModels
        var signals = new List<SignalComponent>();
        int traceIndex = 0;
        int totalTraces = vm.SignalTraces.Count(t => t.Visible);
        foreach (var trace in vm.SignalTraces)
        {
            if (!trace.Visible) continue;

            var file = vm.OpenFiles.FirstOrDefault(f => f.FileIndex == trace.FileIndex);
            if (file == null) continue;

            var sigParam = file.Header.Signals[trace.SignalIndex];
            int signalHeight = totalTraces > 0 ? info.Height / totalTraces : info.Height;

            signals.Add(new SignalComponent
            {
                FileIndex = trace.FileIndex,
                SignalIndex = trace.SignalIndex,
                Label = trace.Label,
                Sensitivity = trace.Sensitivity,
                Polarity = trace.Polarity,
                TraceColor = trace.TraceColor,
                Visible = true,
                Baseline = signalHeight / 2 + traceIndex * signalHeight,
                PhysicalMinimum = sigParam.PhysicalMinimum,
                PhysicalMaximum = sigParam.PhysicalMaximum,
                DigitalMinimum = sigParam.DigitalMinimum,
                DigitalMaximum = sigParam.DigitalMaximum,
                BitValue = sigParam.BitValue,
                Offset = sigParam.Offset,
                SampleFrequency = sigParam.SampleFrequency,
                SamplesPerDataRecord = sigParam.SamplesPerDataRecord,
                PhysicalDimension = sigParam.PhysicalDimension,
            });
            traceIndex++;
        }

        // Render
        try
        {
            _renderer.RenderStage1(signals, vm.OpenFiles.ToList(),
                info.Width, info.Height,
                vm.SignalViewer.ViewTimeTicks, vm.SignalViewer.PageTimeTicks);
            _renderer.RenderStage2(canvas, info.Width, info.Height,
                signals, vm.Settings.Theme,
                new RenderOptions
                {
                    ShowGrid = vm.SignalViewer.ShowGrid,
                    ShowBaselines = vm.SignalViewer.ShowBaselines,
                    AntiAlias = vm.Settings.AntiAliasing,
                    ShowAnnotations = vm.SignalViewer.ShowAnnotations,
                    Crosshair1 = vm.SignalViewer.Crosshair1,
                    Crosshair2 = vm.SignalViewer.Crosshair2,
                });
        }
        catch
        {
            // Silently handle render errors to avoid crashing the UI
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel == null) return;
        _isDragging = true;
        _dragStart = e.GetPosition(this);
        CaptureMouse();

        var crosshair = ViewModel.SignalViewer.Crosshair1;
        crosshair.IsActive = true;
        crosshair.XPixel = _dragStart.X;
        crosshair.YPixel = _dragStart.Y;
        SkiaCanvas.InvalidateVisual();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ReleaseMouseCapture();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (ViewModel == null) return;
        var pos = e.GetPosition(this);

        if (_isDragging)
        {
            var crosshair = ViewModel.SignalViewer.Crosshair1;
            crosshair.XPixel = pos.X;
            crosshair.YPixel = pos.Y;
            SkiaCanvas.InvalidateVisual();
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel == null) return;

        if (e.Delta > 0)
            ViewModel.SignalViewer.ZoomIn();
        else
            ViewModel.SignalViewer.ZoomOut();

        SkiaCanvas.InvalidateVisual();
    }
}
