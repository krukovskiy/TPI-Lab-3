namespace EdfBrowser.App.Services;

using System.Xml.Linq;
using EdfBrowser.App.Models;

public interface IMontageService
{
    MontageModel Load(string filePath);
    void Save(MontageModel montage, string filePath);
}

public sealed class MontageService : IMontageService
{
    public MontageModel Load(string filePath)
    {
        var doc = XDocument.Load(filePath);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid montage file.");

        var montage = new MontageModel
        {
            Name = root.Attribute("name")?.Value ?? Path.GetFileNameWithoutExtension(filePath),
            FilePath = filePath,
        };

        var signalsEl = root.Element("Signals");
        if (signalsEl != null)
        {
            foreach (var se in signalsEl.Elements("Signal"))
            {
                var entry = new MontageSignalEntry
                {
                    SignalLabel = se.Attribute("label")?.Value ?? "",
                    Sensitivity = double.TryParse(se.Attribute("sensitivity")?.Value,
                        System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1.0,
                    ScreenOffset = int.TryParse(se.Attribute("screenOffset")?.Value, out var so) ? so : 0,
                    Polarity = int.TryParse(se.Attribute("polarity")?.Value, out var p) ? p : 1,
                    ColorHex = se.Attribute("color")?.Value ?? "#000000",
                };

                var filtersEl = se.Element("Filters");
                if (filtersEl != null)
                {
                    foreach (var fe in filtersEl.Elements("Filter"))
                    {
                        var filterEntry = new SessionFilterEntry
                        {
                            FilterType = fe.Attribute("type")?.Value ?? "",
                        };
                        foreach (var attr in fe.Attributes().Where(a => a.Name != "type"))
                            filterEntry.Parameters[attr.Name.LocalName] = attr.Value;
                        entry.Filters.Add(filterEntry);
                    }
                }

                montage.Signals.Add(entry);
            }
        }

        return montage;
    }

    public void Save(MontageModel montage, string filePath)
    {
        var doc = new XDocument(
            new XElement("Montage",
                new XAttribute("name", montage.Name),
                new XElement("Signals",
                    montage.Signals.Select(s =>
                        new XElement("Signal",
                            new XAttribute("label", s.SignalLabel),
                            new XAttribute("sensitivity", s.Sensitivity.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                            new XAttribute("screenOffset", s.ScreenOffset),
                            new XAttribute("polarity", s.Polarity),
                            new XAttribute("color", s.ColorHex),
                            new XElement("Filters",
                                s.Filters.Select(f =>
                                {
                                    var el = new XElement("Filter", new XAttribute("type", f.FilterType));
                                    foreach (var kv in f.Parameters)
                                        el.Add(new XAttribute(kv.Key, kv.Value));
                                    return el;
                                }))))))
        );

        doc.Save(filePath);
        montage.FilePath = filePath;
    }
}
