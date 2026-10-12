using System.Text.Json;
using ApplicationLayer.Interfaces;
using CoreModels.DTOs;

namespace BusinessLayer.Parsers;

public sealed class JsonParser : IFileParser
{
    public bool CanParse(string extension) => extension == ".json";

    public async Task<ParsedWorkbook> ParseAsync(Stream stream, string fileName, Token token)
    {
        using var document = await JsonDocument.ParseAsync(stream, default, token);
        var items = document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.EnumerateArray().ToList() : [document.RootElement];
        var objects = items.Where(item => item.ValueKind == JsonValueKind.Object).ToList();
        if (objects.Count == 0) throw new InvalidDataException("The JSON file must contain an object or an array of objects.");
        var names = objects.SelectMany(item => item.EnumerateObject().Select(property => property.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var columns = names.Select((name, order) => new ParsedColumn(name, order)).ToList();
        var rows = objects.Select((item, index) => new ParsedRow(index + 1, columns.Select(column => { var property = item.EnumerateObject().FirstOrDefault(property => property.Name.Equals(column.Name, StringComparison.OrdinalIgnoreCase)); var value = property.Value.ValueKind == JsonValueKind.Undefined || property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.ToString(); return new ParsedCell(column.Order, value, DetectType(property.Value)); }).ToList())).ToList();
        return new ParsedWorkbook([new ParsedSheet(Path.GetFileNameWithoutExtension(fileName), 0, columns, rows)]);
    }

    private static string DetectType(JsonElement value) => value.ValueKind switch { JsonValueKind.Number => "number", JsonValueKind.True or JsonValueKind.False => "boolean", _ => "string" };
}
