namespace EdfBrowser.App.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdfLib.Core;

public sealed partial class HeaderEditorViewModel : ObservableObject
{
    [ObservableProperty] private string _patientId = "";
    [ObservableProperty] private string _recordingId = "";
    [ObservableProperty] private DateTime _startDateTime;
    [ObservableProperty] private int _signalCount;
    [ObservableProperty] private double _dataRecordDuration;
    [ObservableProperty] private long _dataRecordCount;
    [ObservableProperty] private string _format = "";
    [ObservableProperty] private string _filePath = "";

    public ObservableCollection<SignalParameterViewModel> Signals { get; } = new();

    public void LoadFromHeader(EdfHeader header, string filePath)
    {
        FilePath = filePath;
        PatientId = header.PatientId;
        RecordingId = header.RecordingId;
        StartDateTime = header.StartDateTime;
        SignalCount = header.SignalCount;
        DataRecordDuration = header.DataRecordDuration;
        DataRecordCount = header.DataRecordCount;
        Format = header.Format.ToString();

        Signals.Clear();
        foreach (var sig in header.Signals)
        {
            Signals.Add(new SignalParameterViewModel
            {
                Label = sig.Label,
                TransducerType = sig.TransducerType,
                PhysicalDimension = sig.PhysicalDimension,
                PhysicalMin = sig.PhysicalMinimum,
                PhysicalMax = sig.PhysicalMaximum,
                DigitalMin = sig.DigitalMinimum,
                DigitalMax = sig.DigitalMaximum,
                SamplesPerDataRecord = sig.SamplesPerDataRecord,
                Prefiltering = sig.Prefiltering,
            });
        }
    }

    [RelayCommand]
    private void Save() { }
}

public sealed partial class SignalParameterViewModel : ObservableObject
{
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string _transducerType = "";
    [ObservableProperty] private string _physicalDimension = "";
    [ObservableProperty] private double _physicalMin;
    [ObservableProperty] private double _physicalMax;
    [ObservableProperty] private int _digitalMin;
    [ObservableProperty] private int _digitalMax;
    [ObservableProperty] private int _samplesPerDataRecord;
    [ObservableProperty] private string _prefiltering = "";
}
