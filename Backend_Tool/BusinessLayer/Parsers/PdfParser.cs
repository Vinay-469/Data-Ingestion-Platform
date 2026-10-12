using ApplicationLayer.Interfaces;
using CoreModels.DTOs;
using UglyToad.PdfPig;

namespace BusinessLayer.Parsers;

public sealed class PdfParser : IFileParser
{
    public bool CanParse(string extension) => extension == ".pdf";

    public Task<ParsedWorkbook> ParseAsync(Stream stream, string fileName, Token token)
    {
        using var document = PdfDocument.Open(stream);
        var rows = new List<ParsedRow>();
        var rowNumber = 1L;
        foreach (var page in document.GetPages())
        {
            token.ThrowIfCancellationRequested();
            foreach (var line in page.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                rows.Add(new ParsedRow(rowNumber++, [new ParsedCell(0, line, "string")]));
            }
        }

        return Task.FromResult(new ParsedWorkbook([new ParsedSheet(Path.GetFileNameWithoutExtension(fileName), 0, [new ParsedColumn("Text", 0)], rows)]));
    }
}
