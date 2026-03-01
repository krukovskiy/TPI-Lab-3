namespace EdfBrowser.Converters.Common;

public sealed class ConverterRegistry
{
    private readonly List<IFormatConverter> _converters = new();

    public IReadOnlyList<IFormatConverter> AllConverters => _converters;

    public ConverterRegistry()
    {
        _converters.Add(new Import.Ascii2EdfConverter());
        _converters.Add(new Import.Wav2EdfConverter());
        _converters.Add(new Import.Raw2EdfConverter());
        _converters.Add(new Import.Mit2EdfConverter());
        _converters.Add(new Import.ScpEcg2EdfConverter());
        _converters.Add(new Import.Ishne2EdfConverter());
        _converters.Add(new Import.Mortara2EdfConverter());
        _converters.Add(new Import.Emsa2EdfConverter());
        _converters.Add(new Import.Biox2EdfConverter());
        _converters.Add(new Import.Nk2EdfConverter());
        _converters.Add(new Import.Fino2EdfConverter());
        _converters.Add(new Import.Biosemi2BdfPlusConverter());
        _converters.Add(new Import.Unisens2EdfConverter());
        _converters.Add(new Import.Cardiotrak2EdfConverter());
        _converters.Add(new Export.EdfToAsciiExporter());
        _converters.Add(new Export.BdfToEdfConverter());
        _converters.Add(new Export.EdfToBdfConverter());
        _converters.Add(new Export.FilteredSignalExporter());
    }

    public IFormatConverter? FindByExtension(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return _converters.FirstOrDefault(c =>
            c.SupportedExtensions.Any(e => e.TrimStart('.').Equals(ext, StringComparison.OrdinalIgnoreCase)));
    }

    public IFormatConverter? FindByName(string name)
    {
        return _converters.FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public void Register(IFormatConverter converter)
    {
        _converters.Add(converter);
    }
}
