namespace EdfBrowser.Converters.Export;

using EdfBrowser.Converters.Common;
using EdfLib.Core;

public sealed class EdfToBdfConverter : ConverterBase
{
    public override string Name => "EDF to BDF";
    public override string Description => "Converts EDF (16-bit) to BDF (24-bit) format.";
    public override string[] SupportedExtensions => [".edf"];

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

        var format = header.Format == EdfFileFormat.EdfPlus ? EdfFileFormat.BdfPlus : EdfFileFormat.Bdf;
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
            writer.SetDigitalParameters(i, -8388608, 8388607);
            writer.SetTransducerType(i, sig.TransducerType);
            writer.SetPrefiltering(i, sig.Prefiltering);
        }

        for (long rec = 0; rec < header.DataRecordCount; rec++)
        {
            ct.ThrowIfCancellationRequested();
            for (int si = 0; si < signalIndices.Count; si++)
            {
                var sig = header.Signals[signalIndices[si]];
                var buf = new double[sig.SamplesPerDataRecord];
                reader.ReadPhysicalSamples(signalIndices[si], buf, sig.SamplesPerDataRecord);
                writer.WritePhysicalSamples(buf);
            }
            ReportProgress(progress, (int)(100.0 * rec / header.DataRecordCount), "Converting...");
        }

        foreach (var annot in reader.Annotations)
            writer.WriteAnnotation(annot.Onset, annot.Duration, annot.Description);

        ReportProgress(progress, 100, "Done");
    }
}
