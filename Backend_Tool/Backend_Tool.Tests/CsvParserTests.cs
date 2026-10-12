using System.Text;
using BusinessLayer.Parsers;
using NUnit.Framework;

namespace Backend_Tool.Tests;

public sealed class CsvParserTests
{
    [Test]
    public async Task ParseAsync_reads_headers_rows_and_values()
    {
        const string csv = "Facility,Customer Qty\nNorth,10\nSouth,20\n";
        var workbook = await new CsvParser().ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv)), "sample.csv", Token.None);
        Assert.That(workbook.Sheets, Has.Exactly(1).Items);
        Assert.AreEqual("sample", workbook.Sheets[0].Name);
        Assert.AreEqual(2, workbook.Sheets[0].Columns.Count);
        Assert.AreEqual(2, workbook.Sheets[0].Rows.Count);
        Assert.AreEqual("North", workbook.Sheets[0].Rows[0].Cells[0].Value);
        Assert.AreEqual("20", workbook.Sheets[0].Rows[1].Cells[1].Value);
    }

    [Test]
    public async Task ParseAsync_supports_quoted_commas()
    {
        const string csv = "Facility,Description\nNorth,\"Main, North\"\n";
        var workbook = await new CsvParser().ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv)), "sample.csv", Token.None);

        Assert.AreEqual("Main, North", workbook.Sheets[0].Rows[0].Cells[1].Value);
    }

    [Test]
    public void ParseAsync_rejects_empty_files()
    {
        Assert.ThrowsAsync<InvalidDataException>(() => new CsvParser().ParseAsync(new MemoryStream(), "empty.csv", Token.None));
    }
}
