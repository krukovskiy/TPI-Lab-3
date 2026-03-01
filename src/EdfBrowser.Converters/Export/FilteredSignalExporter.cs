namespace EdfBrowser.Converters.Export;

using EdfBrowser.Converters.Common;
using EdfLib.Core;

public sealed class FilteredSignalExporter : ConverterBase
{
    public override string Name => "Filtered Signal Export";
    public override string Description => "Exports filtered signals to a new EDF file (pass-through without filtering by default).";
    public override string[] SupportedExtensions => [".edf", ".bdf"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);

        using var reader = new EdfReader(inputPath);
        var header = reader.Header;

        var signalIndices = new List<int>();
        for (int i = 0; i < header.SignalCount; i++)
            if (!header.Signals[i].IsAnnotationSignal) signalIndices.Add(i);

        using var writer = new EdfWriter(outputPath, header.Format, signalIndices.Count);
        writer.SetPatientInfo(header.PatientId);
        writer.SetRecordingInfo(header.RecordingId);
        writer.SetStartDateTime(header.StartDateTime, header.StartDateTimeSubSecond);
        writer.SetDataRecordDuration(header.DataRecordDuration);

        for (int i = 0; i < signalIndices.Count; i++)
        {
            var sig = header.Signals[signalIndices[i]];
            writer.SetLabel(i, sig.Label);
            writer.SetSampleFrequency(i, sig.SamplesPerDataRecord);
            writer.SetPhysicalParameters(i, sig.PhysicalMinimum, sig.PhysicalMaximum, sig.PhysicalDimension);
            writer.SetDigitalParameters(i, sig.DigitalMinimum, sig.DigitalMaximum);
        }

        for (long rec = 0; rec < header.DataRecordCount; rec++)
        {
            ct.ThrowIfCancellationRequested();
            for (int si = 0; si < signalIndices.Count; si++)
            {
                var sig = header.Signals[signalIndices[si]];
                var buf = new double[sig.SamplesPerDataRecord];
                reader.ReadPhysicalSamples(signalIndices[si], buf, sig.SamplesPerDataRecord);
                // Apply filter chain if provided via options (future extension)
                writer.WritePhysicalSamples(buf);
            }
            ReportProgress(progress, (int)(100.0 * rec / header.DataRecordCount), "Exporting...");
        }

        foreach (var annot in reader.Annotations)
            writer.WriteAnnotation(annot.Onset, annot.Duration, annot.Description);

        ReportProgress(progress, 100, "Done");
    }
}
