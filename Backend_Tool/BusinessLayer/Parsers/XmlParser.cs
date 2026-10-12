using System.Xml.Linq;
using ApplicationLayer.Interfaces;
using CoreModels.DTOs;

namespace BusinessLayer.Parsers;

public sealed class XmlParser : IFileParser
{
    public bool CanParse(string extension) => extension == ".xml";

    public async Task<ParsedWorkbook> ParseAsync(Stream stream, string fileName, Token token)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, token);
        memory.Position = 0;
        var document = await XDocument.LoadAsync(memory, LoadOptions.None, token);
        var root = document.Root ?? throw new InvalidDataException("The XML file has no root element.");
        var records = root.Elements().Where(element => element.Elements().Any() || element.HasAttributes).ToList();
        if (records.Count == 0) records = [root];
        var names = records.SelectMany(record => record.Attributes().Select(attribute => $"@{attribute.Name.LocalName}").Concat(record.Elements().Select(element => element.Name.LocalName))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var columns = names.Select((name, order) => new ParsedColumn(name, order)).ToList();
        var rows = records.Select((record, index) => new ParsedRow(index + 1, columns.Select(column => { var value = column.Name.StartsWith('@') ? record.Attribute(column.Name[1..])?.Value : record.Elements().FirstOrDefault(element => element.Name.LocalName.Equals(column.Name, StringComparison.OrdinalIgnoreCase))?.Value; return new ParsedCell(column.Order, value, decimal.TryParse(value, out _) ? "number" : "string"); }).ToList())).ToList();
        return new ParsedWorkbook([new ParsedSheet(Path.GetFileNameWithoutExtension(fileName), 0, columns, rows)]);
    }
}
