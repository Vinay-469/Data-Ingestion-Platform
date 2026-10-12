using ApplicationLayer.Interfaces;
using CoreModels.DTOs;
using NPOI.XWPF.UserModel;

namespace BusinessLayer.Parsers;

public sealed class DocParser : IFileParser
{
    public bool CanParse(string extension) => extension == ".docx";

    public Task<ParsedWorkbook> ParseAsync(Stream stream, string fileName, Token token)
    {
        return Task.FromResult(ParseDocx(stream, fileName, token));
    }

    private static ParsedWorkbook ParseDocx(Stream stream, string fileName, Token token)
    {
        var document = new XWPFDocument(stream);
        var tables = document.Tables;
        if (tables.Count > 0)
        {
            var table = tables[0];
            var firstRow = table.Rows.FirstOrDefault() ?? throw new InvalidDataException("The DOCX table is empty.");
            var columns = Enumerable.Range(0, firstRow.GetTableCells().Count).Select(index => new ParsedColumn(firstRow.GetCell(index).GetText(), index)).ToList();
            var rows = table.Rows.Skip(1).Select((row, index) => new ParsedRow(index + 2, columns.Select(column => { var value = column.Order < row.GetTableCells().Count ? row.GetCell(column.Order).GetText() : null; return new ParsedCell(column.Order, value, decimal.TryParse(value, out _) ? "number" : "string"); }).ToList())).ToList();
            return new ParsedWorkbook([new ParsedSheet(Path.GetFileNameWithoutExtension(fileName), 0, columns, rows)]);
        }

        var paragraphs = document.Paragraphs.Where(paragraph => !string.IsNullOrWhiteSpace(paragraph.Text)).Select((paragraph, index) => new ParsedRow(index + 1, [new ParsedCell(0, paragraph.Text, "string")])).ToList();
        return new ParsedWorkbook([new ParsedSheet(Path.GetFileNameWithoutExtension(fileName), 0, [new ParsedColumn("Text", 0)], paragraphs)]);
    }
}
