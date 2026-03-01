namespace EdfBrowser.App.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdfBrowser.Dsp.Design;
using EdfBrowser.Dsp.Filters;

public sealed partial class FilterDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _selectedFilterType = "Highpass";
    [ObservableProperty] private double _frequency1 = 0.5;
    [ObservableProperty] private double _frequency2 = 40.0;
    [ObservableProperty] private int _filterOrder = 2;
    [ObservableProperty] private int _selectedSignalIndex;
    [ObservableProperty] private double _sampleRate;

    public ObservableCollection<string> FilterTypes { get; } = new()
    {
        "Highpass", "Lowpass", "Bandpass", "Bandstop", "Notch", "Spike", "Running Average"
    };

    public ObservableCollection<FilterItemViewModel> ActiveFilters { get; } = new();

    public bool DialogResult { get; set; }

    [RelayCommand]
    private void AddFilter()
    {
        if (ActiveFilters.Count >= 16) return;

        IFilter? filter = SelectedFilterType switch
        {
            "Highpass" => new FirstOrderIirFilter(FilterType.HighPass, _frequency1, _sampleRate),
            "Lowpass" => new FirstOrderIirFilter(FilterType.LowPass, _frequency1, _sampleRate),
            "Bandpass" => ButterworthDesigner.DesignCascade(IirFilterType.BandPass, _filterOrder, _frequency1, _frequency2, _sampleRate).FirstOrDefault(),
            "Bandstop" => ButterworthDesigner.DesignCascade(IirFilterType.BandStop, _filterOrder, _frequency1, _frequency2, _sampleRate).FirstOrDefault(),
            "Notch" => ButterworthDesigner.DesignCascade(IirFilterType.BandStop, 2, _frequency1 - 1, _frequency1 + 1, _sampleRate).FirstOrDefault(),
            "Spike" => new SpikeFilter(_frequency1, (int)_frequency2, _sampleRate),
            "Running Average" => new RunningAverageFilter((int)_frequency1, 0),
            _ => null
        };

        if (filter != null)
        {
            ActiveFilters.Add(new FilterItemViewModel
            {
                Filter = filter,
                TypeName = SelectedFilterType,
                Description = $"{SelectedFilterType} {_frequency1:F1} Hz"
            });
        }
    }

    [RelayCommand]
    private void RemoveFilter(int index)
    {
        if (index >= 0 && index < ActiveFilters.Count)
        {
            ActiveFilters[index].Filter.Dispose();
            ActiveFilters.RemoveAt(index);
        }
    }

    [RelayCommand]
    private void ClearAllFilters()
    {
        foreach (var f in ActiveFilters)
            f.Filter.Dispose();
        ActiveFilters.Clear();
    }

    [RelayCommand]
    private void Apply() => DialogResult = true;

    [RelayCommand]
    private void Cancel() => DialogResult = false;
}

public sealed partial class FilterItemViewModel : ObservableObject
{
    [ObservableProperty] private string _typeName = "";
    [ObservableProperty] private string _description = "";
    public IFilter Filter { get; set; } = null!;
}
