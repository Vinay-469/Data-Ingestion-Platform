using ApplicationLayer.Interfaces;
using CoreModels.DTOs;
using NPOI.SS.UserModel;

namespace BusinessLayer.Parsers;

public sealed class ExcelParser : IFileParser
{
    public bool CanParse(string extension) => extension is ".xlsx" or ".xls";

    public Task<ParsedWorkbook> ParseAsync(Stream stream, string fileName, Token token)
    {
        var workbook = WorkbookFactory.Create(stream);
        var sheets = new List<ParsedSheet>();
        for (var sheetIndex = 0; sheetIndex < workbook.NumberOfSheets; sheetIndex++)
        {
            var sheet = workbook.GetSheetAt(sheetIndex);
            var headerRow = sheet.FirstRowNum <= sheet.LastRowNum ? sheet.GetRow(sheet.FirstRowNum) : null;
            if (headerRow is null) { sheets.Add(new ParsedSheet(sheet.SheetName, sheetIndex, [], [])); continue; }
            var columns = Enumerable.Range(0, headerRow.LastCellNum).Select(index => new ParsedColumn(GetText(headerRow.GetCell(index)), index)).ToList();
            var rows = new List<ParsedRow>();
            for (var rowIndex = headerRow.RowNum + 1; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                token.ThrowIfCancellationRequested();
                var row = sheet.GetRow(rowIndex);
                if (row is null) continue;
                var cells = columns.Select(column => ToCell(row.GetCell(column.Order), column.Order)).ToList();
                if (cells.All(cell => string.IsNullOrWhiteSpace(cell.Value))) continue;
                rows.Add(new ParsedRow(rowIndex, cells));
            }
            sheets.Add(new ParsedSheet(sheet.SheetName, sheetIndex, columns, rows));
        }
        return Task.FromResult(new ParsedWorkbook(sheets));
    }

    private static ParsedCell ToCell(ICell? cell, int order)
    {
        if (cell is null) return new ParsedCell(order, null, "string");
        var value = GetText(cell);
        var type = cell.CellType switch { CellType.Numeric when DateUtil.IsCellDateFormatted(cell) => "date", CellType.Numeric => "number", CellType.Boolean => "boolean", _ => "string" };
        return new ParsedCell(order, value, type);
    }

    private static string GetText(ICell? cell) => cell is null ? string.Empty : cell.CellType switch
    {
        CellType.Numeric when DateUtil.IsCellDateFormatted(cell) => Convert.ToDateTime(cell.DateCellValue).ToString("O"),
        CellType.Numeric => cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        CellType.Boolean => cell.BooleanCellValue.ToString(),
        _ => cell.ToString() ?? string.Empty
    };
}
