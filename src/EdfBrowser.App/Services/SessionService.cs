namespace EdfBrowser.App.Services;

using System.Xml.Linq;
using EdfBrowser.App.Models;

public interface ISessionService
{
    SessionModel Load(string filePath);
    void Save(SessionModel session, string filePath);
}

public sealed class SessionService : ISessionService
{
    public SessionModel Load(string filePath)
    {
        var doc = XDocument.Load(filePath);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid session file.");
        var session = new SessionModel { SessionFilePath = filePath };

        var filesEl = root.Element("Files");
        if (filesEl != null)
        {
            foreach (var fe in filesEl.Elements("File"))
            {
                session.Files.Add(new SessionFileEntry
                {
                    FilePath = fe.Attribute("path")?.Value ?? "",
                    ViewTimeOffsetTicks = long.TryParse(fe.Attribute("offset")?.Value, out var o) ? o : 0
                });
            }
        }

        var signalsEl = root.Element("Signals");
        if (signalsEl != null)
        {
            foreach (var se in signalsEl.Elements("Signal"))
            {
                var entry = new SessionSignalEntry
                {
                    FileIndex = int.TryParse(se.Attribute("fileIndex")?.Value, out var fi) ? fi : 0,
                    SignalIndex = int.TryParse(se.Attribute("signalIndex")?.Value, out var si) ? si : 0,
                    Sensitivity = double.TryParse(se.Attribute("sensitivity")?.Value, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1.0,
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
                        {
                            filterEntry.Parameters[attr.Name.LocalName] = attr.Value;
                        }
                        entry.Filters.Add(filterEntry);
                    }
                }

                session.Signals.Add(entry);
            }
        }

        var viewEl = root.Element("View");
        if (viewEl != null)
        {
            session.ViewTimeTicks = long.TryParse(viewEl.Attribute("viewTime")?.Value, out var vt) ? vt : 0;
            session.PageTimeTicks = long.TryParse(viewEl.Attribute("pageTime")?.Value, out var pt) ? pt : 100_000_000;
        }

        return session;
    }

    public void Save(SessionModel session, string filePath)
    {
        var doc = new XDocument(
            new XElement("Session",
                new XElement("Files",
                    session.Files.Select(f =>
                        new XElement("File",
                            new XAttribute("path", f.FilePath),
                            new XAttribute("offset", f.ViewTimeOffsetTicks)))),
                new XElement("Signals",
                    session.Signals.Select(s =>
                        new XElement("Signal",
                            new XAttribute("fileIndex", s.FileIndex),
                            new XAttribute("signalIndex", s.SignalIndex),
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
                                }))))),
                new XElement("View",
                    new XAttribute("viewTime", session.ViewTimeTicks),
                    new XAttribute("pageTime", session.PageTimeTicks))
            )
        );

        doc.Save(filePath);
        session.SessionFilePath = filePath;
    }
}
