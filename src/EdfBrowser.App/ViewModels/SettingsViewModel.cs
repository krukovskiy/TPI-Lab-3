namespace EdfBrowser.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdfBrowser.App.Models;

public sealed partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private bool _linearInterpolation = true;
    [ObservableProperty] private bool _showBaselines = true;
    [ObservableProperty] private bool _showGrid = true;
    [ObservableProperty] private bool _antiAliasing = true;
    [ObservableProperty] private double _defaultPageTime = 10.0;
    [ObservableProperty] private bool _showAnnotationMarkers = true;
    [ObservableProperty] private int _annotationFontSize = 10;
    [ObservableProperty] private bool _remoteControlEnabled;
    [ObservableProperty] private int _remoteControlPort = 40100;
    [ObservableProperty] private int _printDpi = 300;
    [ObservableProperty] private bool _printGrayscale;

    public bool DialogResult { get; set; }

    public void LoadFrom(AppSettings settings)
    {
        LinearInterpolation = settings.LinearInterpolation;
        ShowBaselines = settings.ShowBaselines;
        ShowGrid = settings.ShowGrid;
        AntiAliasing = settings.AntiAliasing;
        DefaultPageTime = settings.DefaultPageTimeSec;
        ShowAnnotationMarkers = settings.ShowAnnotationMarkers;
        AnnotationFontSize = settings.AnnotationFontSize;
        RemoteControlEnabled = settings.RemoteControlEnabled;
        RemoteControlPort = settings.RemoteControlPort;
        PrintDpi = settings.PrintDpi;
        PrintGrayscale = settings.PrintGrayscale;
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.LinearInterpolation = LinearInterpolation;
        settings.ShowBaselines = ShowBaselines;
        settings.ShowGrid = ShowGrid;
        settings.AntiAliasing = AntiAliasing;
        settings.DefaultPageTimeSec = DefaultPageTime;
        settings.ShowAnnotationMarkers = ShowAnnotationMarkers;
        settings.AnnotationFontSize = AnnotationFontSize;
        settings.RemoteControlEnabled = RemoteControlEnabled;
        settings.RemoteControlPort = RemoteControlPort;
        settings.PrintDpi = PrintDpi;
        settings.PrintGrayscale = PrintGrayscale;
    }

    [RelayCommand]
    private void Apply() => DialogResult = true;

    [RelayCommand]
    private void RestoreDefaults()
    {
        var defaults = new AppSettings();
        LoadFrom(defaults);
    }
}
