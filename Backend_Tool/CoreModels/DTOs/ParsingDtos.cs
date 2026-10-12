namespace CoreModels.DTOs;

public sealed record ParsedWorkbook(IReadOnlyList<ParsedSheet> Sheets);

public sealed record ParsedSheet(
    string Name,
    int Order,
    IReadOnlyList<ParsedColumn> Columns,
    IReadOnlyList<ParsedRow> Rows);

public sealed record ParsedColumn(string Name, int Order);

public sealed record ParsedRow(long Number, IReadOnlyList<ParsedCell> Cells);

public sealed record ParsedCell(int ColumnOrder, string? Value, string DataType);
