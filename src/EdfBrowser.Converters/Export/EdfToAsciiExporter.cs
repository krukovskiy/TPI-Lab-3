namespace EdfBrowser.Converters.Export;

using System.Globalization;
using System.Text;
using EdfBrowser.Converters.Common;
using EdfLib.Core;

public sealed class EdfToAsciiExporter : ConverterBase
{
    public override string Name => "EDF to ASCII";
    public override string Description => "Exports EDF/BDF signals to CSV/TSV text format.";
    public override string[] SupportedExtensions => [".edf", ".bdf"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);
        options ??= new ConverterOptions();
        string sep = options.Separator ?? ",";

        ReportProgress(progress, 0, "Reading EDF file...");

        using var reader = new EdfReader(inputPath);
        var header = reader.Header;

        // Find non-annotation signal indices
        var signalIndices = new List<int>();
        for (int i = 0; i < header.SignalCount; i++)
        {
            if (!header.Signals[i].IsAnnotationSignal)
                signalIndices.Add(i);
        }

        if (signalIndices.Count == 0) throw new InvalidDataException("No data signals found.");

        int maxSamples = signalIndices.Max(i => (int)(header.DataRecordCount * header.Signals[i].SamplesPerDataRecord));

        ReportProgress(progress, 10, "Writing ASCII...");

        await using var outStream = File.CreateText(outputPath);

        // Write header
        if (options.HasHeader)
        {
            var labels = signalIndices.Select(i => header.Signals[i].Label.Trim());
            bool includeTime = options.CustomOptions.ContainsKey("includeTime");
            if (includeTime)
                await outStream.WriteLineAsync("Time" + sep + string.Join(sep, labels));
            else
                await outStream.WriteLineAsync(string.Join(sep, labels));
        }

        // Read all signal data
        var signalData = new double[signalIndices.Count][];
        for (int si = 0; si < signalIndices.Count; si++)
        {
            int idx = signalIndices[si];
            int total = (int)(header.DataRecordCount * header.Signals[idx].SamplesPerDataRecord);
            signalData[si] = new double[total];
            reader.ReadPhysicalSamples(idx, signalData[si], total);
            reader.Rewind(idx);
        }

        // Write data rows
        var sb = new StringBuilder();
        for (int s = 0; s < maxSamples; s++)
        {
            ct.ThrowIfCancellationRequested();
            sb.Clear();
            for (int si = 0; si < signalIndices.Count; si++)
            {
                if (si > 0) sb.Append(sep);
                double val = s < signalData[si].Length ? signalData[si][s] : 0;
                sb.Append(val.ToString("G8", CultureInfo.InvariantCulture));
            }
            await outStream.WriteLineAsync(sb.ToString());

            if (s % 10000 == 0)
                ReportProgress(progress, 10 + (int)(90.0 * s / maxSamples), "Writing...");
        }

        ReportProgress(progress, 100, "Done");
    }
}
