using System.Text;
using ApplicationLayer.Interfaces;
using CoreModels.DTOs;

namespace BusinessLayer.Parsers;

public sealed class CsvParser : IFileParser
{
    public bool CanParse(string extension) => extension == ".csv";

    public async Task<ParsedWorkbook> ParseAsync(Stream stream, string fileName, Token token)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var headerLine = await reader.ReadLineAsync(token) ?? throw new InvalidDataException("The CSV file is empty.");
        var headers = ParseLine(headerLine);
        var columns = headers.Select((name, order) => new ParsedColumn(name.Trim(), order)).ToList();
        var rows = new List<ParsedRow>();
        var rowNumber = 1L;
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(token);
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = ParseLine(line);
            rows.Add(new ParsedRow(rowNumber, columns.Select(column => { var value = column.Order < values.Count ? values[column.Order] : null; return new ParsedCell(column.Order, value, "string"); }).ToList()));
        }
        return new ParsedWorkbook([new ParsedSheet(Path.GetFileNameWithoutExtension(fileName), 0, columns, rows)]);
    }

    private static List<string> ParseLine(string line)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        { var c = line[i];
            if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"')
            { 
                builder.Append('"'); i++; 
            }
            else if (c == '"') quoted = !quoted;
            else if (c == ',' && !quoted)
            {
                values.Add(builder.ToString());
                builder.Clear(); 
            } 
            else builder.Append(c);
        }
        values.Add(builder.ToString()); return values;
    }
}
