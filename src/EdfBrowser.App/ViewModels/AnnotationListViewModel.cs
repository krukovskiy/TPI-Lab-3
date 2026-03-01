using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EdfBrowser.App.Infrastructure;
using EdfLib.Core;

namespace EdfBrowser.App.ViewModels;

/// <summary>
/// ViewModel for the annotation dock panel.
/// Manages the list of annotations with filtering, selection, and navigation.
/// </summary>
public sealed partial class AnnotationListViewModel : ObservableObject
{
    private readonly ICollectionView _filteredAnnotations;

    public AnnotationListViewModel()
    {
        _filteredAnnotations = CollectionViewSource.GetDefaultView(Annotations);
        _filteredAnnotations.Filter = FilterAnnotation;
    }

    /// <summary>All annotation items loaded from the current file.</summary>
    public ObservableCollection<AnnotationItemViewModel> Annotations { get; } = new();

    /// <summary>Currently selected annotation item.</summary>
    [ObservableProperty]
    private AnnotationItemViewModel? _selectedAnnotation;

    /// <summary>Text filter for the annotation list.</summary>
    [ObservableProperty]
    private string _filterText = "";

    /// <summary>Total number of annotations (unfiltered).</summary>
    [ObservableProperty]
    private int _annotationCount;

    /// <summary>Filtered view of annotations, bound by the UI.</summary>
    public ICollectionView FilteredAnnotations => _filteredAnnotations;

    partial void OnFilterTextChanged(string value)
    {
        _filteredAnnotations.Refresh();
    }

    /// <summary>
    /// Loads annotations from the given list into the ViewModel.
    /// </summary>
    public void LoadAnnotations(IReadOnlyList<EdfAnnotation> annotations, int fileIndex = 0)
    {
        Annotations.Clear();

        for (int i = 0; i < annotations.Count; i++)
        {
            var ann = annotations[i];
            Annotations.Add(new AnnotationItemViewModel
            {
                OnsetTicks = ann.Onset.Ticks,
                DurationTicks = ann.Duration?.Ticks,
                Description = ann.Description,
                FileIndex = fileIndex,
                AnnotationIndex = i,
            });
        }

        AnnotationCount = Annotations.Count;
        _filteredAnnotations.Refresh();
    }

    /// <summary>
    /// Clears all annotations from the list.
    /// </summary>
    public void ClearAnnotations()
    {
        Annotations.Clear();
        AnnotationCount = 0;
        SelectedAnnotation = null;
    }

    /// <summary>
    /// Navigates the signal viewer to the selected annotation's onset time.
    /// </summary>
    [RelayCommand]
    private void NavigateToAnnotation()
    {
        if (SelectedAnnotation is null) return;

        WeakReferenceMessenger.Default.Send(
            new AnnotationSelectedMessage(SelectedAnnotation.FileIndex, SelectedAnnotation.AnnotationIndex));
    }

    /// <summary>
    /// Opens the edit UI for the selected annotation.
    /// </summary>
    [RelayCommand]
    private void EditAnnotation()
    {
        if (SelectedAnnotation is null) return;
        // In a full implementation this would open an edit dialog.
        // For now, the description is directly editable via binding.
    }

    /// <summary>
    /// Deletes the selected annotation.
    /// </summary>
    [RelayCommand]
    private void DeleteAnnotation()
    {
        if (SelectedAnnotation is null) return;

        var ann = SelectedAnnotation;
        WeakReferenceMessenger.Default.Send(
            new AnnotationDeletedMessage(ann.FileIndex, ann.AnnotationIndex));

        Annotations.Remove(ann);
        AnnotationCount = Annotations.Count;
    }

    /// <summary>
    /// Exports annotations to a CSV file.
    /// </summary>
    [RelayCommand]
    private void ExportAnnotations()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Annotations",
            Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt",
            DefaultExt = ".csv",
        };

        if (dialog.ShowDialog() == true)
        {
            using var writer = new System.IO.StreamWriter(dialog.FileName);
            writer.WriteLine("Onset,Duration,Description");
            foreach (var ann in Annotations)
            {
                string onset = TimeSpan.FromTicks(ann.OnsetTicks).ToString(@"hh\:mm\:ss\.fff");
                string duration = ann.DurationTicks.HasValue
                    ? $"{(double)ann.DurationTicks.Value / EdfTime.TicksPerSecond:F3}"
                    : "";
                string desc = ann.Description.Replace("\"", "\"\"");
                writer.WriteLine($"\"{onset}\",\"{duration}\",\"{desc}\"");
            }
        }
    }

    private bool FilterAnnotation(object obj)
    {
        if (string.IsNullOrWhiteSpace(_filterText)) return true;
        if (obj is not AnnotationItemViewModel item) return false;
        return item.Description.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
            || item.OnsetText.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// ViewModel for a single annotation item in the list.
/// </summary>
public sealed partial class AnnotationItemViewModel : ObservableObject
{
    /// <summary>Onset time in 100-ns ticks from recording start.</summary>
    [ObservableProperty]
    private long _onsetTicks;

    /// <summary>Duration in 100-ns ticks, or null if unspecified.</summary>
    [ObservableProperty]
    private long? _durationTicks;

    /// <summary>Annotation description text.</summary>
    [ObservableProperty]
    private string _description = "";

    /// <summary>Whether this annotation is currently selected in the viewer.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Formatted onset time string.</summary>
    public string OnsetText => TimeSpan.FromTicks(_onsetTicks).ToString(@"hh\:mm\:ss\.fff");

    /// <summary>Formatted duration string.</summary>
    public string DurationText => _durationTicks.HasValue
        ? $"{(double)_durationTicks.Value / EdfTime.TicksPerSecond:F3}s"
        : "";

    /// <summary>File index this annotation belongs to.</summary>
    public int FileIndex { get; set; }

    /// <summary>Index of this annotation within the file's annotation list.</summary>
    public int AnnotationIndex { get; set; }

    partial void OnOnsetTicksChanged(long value)
    {
        OnPropertyChanged(nameof(OnsetText));
    }

    partial void OnDurationTicksChanged(long? value)
    {
        OnPropertyChanged(nameof(DurationText));
    }
}
