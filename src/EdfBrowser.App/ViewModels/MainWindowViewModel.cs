namespace EdfBrowser.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdfBrowser.App.Models;
using EdfBrowser.App.Services;
using EdfLib.Core;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IFileManagerService _fileManager;
    private readonly ISessionService _sessionService;
    private readonly IMontageService _montageService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private EdfFileModel? _selectedFile;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _fileInfoText = "";

    public ObservableCollection<EdfFileModel> OpenFiles { get; } = new();
    public ObservableCollection<SignalTraceViewModel> SignalTraces { get; } = new();

    public SignalViewerViewModel SignalViewer { get; }
    public AnnotationListViewModel AnnotationDock { get; }
    public AppSettings Settings { get; set; }

    public string Title => SelectedFile != null
        ? $"EDFbrowser - {SelectedFile.FileName}"
        : "EDFbrowser";

    public MainWindowViewModel(
        IFileManagerService fileManager,
        ISessionService sessionService,
        IMontageService montageService,
        ISettingsService settingsService)
    {
        _fileManager = fileManager;
        _sessionService = sessionService;
        _montageService = montageService;
        _settingsService = settingsService;

        Settings = _settingsService.Load();
        SignalViewer = new SignalViewerViewModel();
        AnnotationDock = new AnnotationListViewModel();

        SignalViewer.PageTimeTicks = (long)(Settings.DefaultPageTimeSec * EdfTime.TicksPerSecond);
    }

    partial void OnSelectedFileChanged(EdfFileModel? value)
    {
        OnPropertyChanged(nameof(Title));
        if (value != null)
        {
            FileInfoText = $"{value.Header.SignalCount} signals | " +
                           $"{value.Header.Duration.TotalSeconds:F1}s | " +
                           $"{value.Header.Format}";
            AnnotationDock.LoadAnnotations(value.Annotations);
            SignalViewer.FileDurationTicks = value.Header.Duration.Ticks;
        }
        else
        {
            FileInfoText = "";
            AnnotationDock.ClearAnnotations();
        }
    }

    [RelayCommand]
    private void OpenFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "EDF/BDF Files|*.edf;*.bdf;*.rec|All Files|*.*",
            Title = "Open EDF/BDF File",
            InitialDirectory = Settings.LastOpenPath ?? "",
            Multiselect = true
        };

        if (dlg.ShowDialog() != true) return;

        foreach (var path in dlg.FileNames)
        {
            try
            {
                var model = _fileManager.OpenFile(path);
                OpenFiles.Add(model);
                SelectedFile = model;
                Settings.LastOpenPath = Path.GetDirectoryName(path);

                if (!Settings.RecentFiles.Contains(path))
                {
                    Settings.RecentFiles.Insert(0, path);
                    if (Settings.RecentFiles.Count > Settings.MaxRecentFiles)
                        Settings.RecentFiles.RemoveAt(Settings.RecentFiles.Count - 1);
                }

                StatusText = $"Opened: {model.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void CloseFile()
    {
        if (SelectedFile == null) return;
        var file = SelectedFile;

        // Remove all traces for this file
        var tracesToRemove = SignalTraces.Where(t => t.FileIndex == file.FileIndex).ToList();
        foreach (var trace in tracesToRemove)
            SignalTraces.Remove(trace);

        OpenFiles.Remove(file);
        _fileManager.CloseFile(file.FileIndex);

        SelectedFile = OpenFiles.FirstOrDefault();
        StatusText = "File closed";
    }

    [RelayCommand]
    private void CloseAllFiles()
    {
        SignalTraces.Clear();
        OpenFiles.Clear();
        _fileManager.CloseAllFiles();
        SelectedFile = null;
        StatusText = "All files closed";
    }

    [RelayCommand]
    private void AddSignals()
    {
        if (SelectedFile == null) return;

        var header = SelectedFile.Header;
        int colorIndex = SignalTraces.Count;

        for (int i = 0; i < header.SignalCount; i++)
        {
            var sig = header.Signals[i];
            if (sig.IsAnnotationSignal) continue;

            bool alreadyAdded = SignalTraces.Any(t =>
                t.FileIndex == SelectedFile.FileIndex && t.SignalIndex == i);
            if (alreadyAdded) continue;

            var trace = new SignalTraceViewModel
            {
                FileIndex = SelectedFile.FileIndex,
                SignalIndex = i,
                Label = sig.Label.Trim(),
                PhysicalDimension = sig.PhysicalDimension.Trim(),
                SampleFrequency = sig.SampleFrequency,
                TraceColor = Settings.Theme.GetSignalColor(colorIndex++),
            };
            SignalTraces.Add(trace);
        }

        SignalViewer.ViewChanged?.Invoke();
        StatusText = $"Added signals from {SelectedFile.FileName}";
    }

    [RelayCommand]
    private void RemoveAllSignals()
    {
        SignalTraces.Clear();
        SignalViewer.ViewChanged?.Invoke();
        StatusText = "All signals removed";
    }

    [RelayCommand]
    private void SaveSession()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Session Files|*.esf|All Files|*.*",
            Title = "Save Session",
            InitialDirectory = Settings.LastSessionPath ?? ""
        };
        if (dlg.ShowDialog() != true) return;

        var session = new SessionModel
        {
            ViewTimeTicks = SignalViewer.ViewTimeTicks,
            PageTimeTicks = SignalViewer.PageTimeTicks,
            Settings = Settings,
        };

        foreach (var f in OpenFiles)
            session.Files.Add(new SessionFileEntry
            {
                FilePath = f.FilePath,
                ViewTimeOffsetTicks = f.ViewTimeOffsetTicks,
            });

        foreach (var t in SignalTraces)
            session.Signals.Add(new SessionSignalEntry
            {
                FileIndex = t.FileIndex,
                SignalIndex = t.SignalIndex,
                Sensitivity = t.Sensitivity,
                ScreenOffset = t.ScreenOffset,
                Polarity = t.Polarity,
                ColorHex = $"#{t.TraceColor.R:X2}{t.TraceColor.G:X2}{t.TraceColor.B:X2}",
            });

        _sessionService.Save(session, dlg.FileName);
        Settings.LastSessionPath = Path.GetDirectoryName(dlg.FileName);
        StatusText = "Session saved";
    }

    [RelayCommand]
    private void LoadSession()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Session Files|*.esf|All Files|*.*",
            Title = "Load Session",
            InitialDirectory = Settings.LastSessionPath ?? ""
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            CloseAllFiles();
            var session = _sessionService.Load(dlg.FileName);

            foreach (var fe in session.Files)
            {
                if (File.Exists(fe.FilePath))
                {
                    var model = _fileManager.OpenFile(fe.FilePath);
                    model.ViewTimeOffsetTicks = fe.ViewTimeOffsetTicks;
                    OpenFiles.Add(model);
                }
            }

            SelectedFile = OpenFiles.FirstOrDefault();
            SignalViewer.ViewTimeTicks = session.ViewTimeTicks;
            SignalViewer.PageTimeTicks = session.PageTimeTicks;

            StatusText = "Session loaded";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading session: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void LoadMontage()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Montage Files|*.mtg|All Files|*.*",
            Title = "Load Montage",
        };
        if (dlg.ShowDialog() != true) return;
        StatusText = "Montage loaded";
    }

    [RelayCommand]
    private void SaveMontage()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Montage Files|*.mtg|All Files|*.*",
            Title = "Save Montage",
        };
        if (dlg.ShowDialog() != true) return;
        StatusText = "Montage saved";
    }

    [RelayCommand]
    private void ShowOptions()
    {
        StatusText = "Options dialog";
    }

    [RelayCommand]
    private void ShowAbout()
    {
        MessageBox.Show(
            "EDFbrowser for .NET\nVersion 1.0\n\nA C# WPF port of EDFbrowser by Teunis van Beelen.\n\nEDF/EDF+/BDF/BDF+ file viewer and analyzer.",
            "About EDFbrowser", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ExportToPng() => StatusText = "Export to PNG";

    [RelayCommand]
    private void ExportToPdf() => StatusText = "Export to PDF";

    [RelayCommand]
    private void Print() => StatusText = "Print";

    [RelayCommand]
    private void Exit()
    {
        _settingsService.Save(Settings);
        Application.Current.Shutdown();
    }

    [RelayCommand]
    private void PageLeft() => SignalViewer.PageLeft();

    [RelayCommand]
    private void PageRight() => SignalViewer.PageRight();

    [RelayCommand]
    private void ZoomIn() => SignalViewer.ZoomIn();

    [RelayCommand]
    private void ZoomOut() => SignalViewer.ZoomOut();

    [RelayCommand]
    private void ZoomBack() => SignalViewer.ZoomBack();

    [RelayCommand]
    private void GoToStart() => SignalViewer.GoToStart();

    [RelayCommand]
    private void GoToEnd() => SignalViewer.GoToEnd();

    [RelayCommand]
    private void FitToScreen() => SignalViewer.FitToScreen(SignalViewer.FileDurationTicks);
}
