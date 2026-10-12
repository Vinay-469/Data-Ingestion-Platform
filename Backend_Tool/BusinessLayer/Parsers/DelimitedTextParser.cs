using System.Text;
using ApplicationLayer.Interfaces;
using CoreModels.DTOs;

namespace BusinessLayer.Parsers;

public sealed class DelimitedTextParser : IFileParser
{
    public bool CanParse(string extension) => extension is ".tsv" or ".txt";

    public async Task<ParsedWorkbook> ParseAsync(Stream stream, string fileName, Token token)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var firstLine = await reader.ReadLineAsync(token) ?? throw new InvalidDataException("The text file is empty.");
        var delimiter = Path.GetExtension(fileName).Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : DetectDelimiter(firstLine);
        var headers = ParseLine(firstLine, delimiter);
        var columns = headers.Select((name, order) => new ParsedColumn(string.IsNullOrWhiteSpace(name) ? $"Column{order + 1}" : name.Trim(), order)).ToList();
        var rows = new List<ParsedRow>();
        var rowNumber = 1L;
        while (!reader.EndOfStream)
        {
            token.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(token);
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = ParseLine(line, delimiter);
            rows.Add(new ParsedRow(rowNumber, columns.Select(column => { var value = column.Order < values.Count ? values[column.Order] : null; return new ParsedCell(column.Order, value, DetectType(value)); }).ToList()));
        }

        return new ParsedWorkbook([new ParsedSheet(Path.GetFileNameWithoutExtension(fileName), 0, columns, rows)]);
    }

    private static char DetectDelimiter(string line) => new[] { '\t', '|', ',', ';' }.OrderByDescending(candidate => line.Count(character => character == candidate)).First();
    private static string DetectType(string? value) => decimal.TryParse(value, out _) ? "number" : "string";
    private static List<string> ParseLine(string line, char delimiter) => line.Split(delimiter).Select(value => value.Trim().Trim('"')).ToList();
}
