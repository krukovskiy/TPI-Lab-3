namespace EdfBrowser.Converters.Export;

using EdfBrowser.Converters.Common;
using EdfLib.Core;

public sealed class BdfToEdfConverter : ConverterBase
{
    public override string Name => "BDF to EDF";
    public override string Description => "Converts BDF (24-bit) to EDF (16-bit) format.";
    public override string[] SupportedExtensions => [".bdf"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);
        ReportProgress(progress, 0, "Reading BDF file...");

        using var reader = new EdfReader(inputPath);
        var header = reader.Header;

        if (header.Format != EdfFileFormat.Bdf && header.Format != EdfFileFormat.BdfPlus)
            throw new InvalidDataException("Input file is not BDF format.");

        var signalIndices = new List<int>();
        for (int i = 0; i < header.SignalCount; i++)
            if (!header.Signals[i].IsAnnotationSignal)
                signalIndices.Add(i);

        ReportProgress(progress, 10, "Writing EDF...");

        var format = header.Format == EdfFileFormat.BdfPlus ? EdfFileFormat.EdfPlus : EdfFileFormat.Edf;
        using var writer = new EdfWriter(outputPath, format, signalIndices.Count);

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
            writer.SetDigitalParameters(i, -32768, 32767);
            writer.SetTransducerType(i, sig.TransducerType);
            writer.SetPrefiltering(i, sig.Prefiltering);
        }

        // Convert data records
        long totalRecords = header.DataRecordCount;
        for (long rec = 0; rec < totalRecords; rec++)
        {
            ct.ThrowIfCancellationRequested();
            for (int si = 0; si < signalIndices.Count; si++)
            {
                int idx = signalIndices[si];
                int spr = header.Signals[idx].SamplesPerDataRecord;
                var buffer = new double[spr];
                reader.ReadPhysicalSamples(idx, buffer, spr);
                writer.WritePhysicalSamples(buffer);
            }
            ReportProgress(progress, 10 + (int)(90.0 * rec / totalRecords), "Converting...");
        }

        // Copy annotations
        foreach (var annot in reader.Annotations)
            writer.WriteAnnotation(annot.Onset, annot.Duration, annot.Description);

        ReportProgress(progress, 100, "Done");
    }
}
