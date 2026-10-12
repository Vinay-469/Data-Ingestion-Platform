using System.IO.Compression;
using System.Text;
using ApplicationLayer.Interfaces;
using BusinessLayer.Parsers;
using Microsoft.Extensions.DependencyInjection;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NUnit.Framework;

namespace Backend_Tool.Tests;

public sealed class ParserCoverageTests
{
    [Test]
    public async Task DelimitedTextParser_reads_tsv_and_missing_values()
    {
        var workbook = await new DelimitedTextParser().ParseAsync(
            Text("Name\tQuantity\nA\t10\nB"), "items.tsv", Token.None);

        Assert.That(workbook.Sheets, Has.Exactly(1).Items);
        var sheet = workbook.Sheets[0];
        Assert.AreEqual(2, sheet.Columns.Count);
        Assert.AreEqual(2, sheet.Rows.Count);
        Assert.AreEqual("number", sheet.Rows[0].Cells[1].DataType);
        Assert.IsNull(sheet.Rows[1].Cells[1].Value);
    }

    [Test]
    public async Task DelimitedTextParser_uses_detected_pipe_delimiter()
    {
        var workbook = await new DelimitedTextParser().ParseAsync(Text("A|B\n1|2"), "items.txt", Token.None);
        Assert.AreEqual("1", workbook.Sheets[0].Rows[0].Cells[0].Value);
        Assert.AreEqual("2", workbook.Sheets[0].Rows[0].Cells[1].Value);
    }

    [Test]
    public void DelimitedTextParser_honours_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(() =>
            new DelimitedTextParser().ParseAsync(Text("A\n1"), "items.tsv", cancellation.Token));
    }

    [Test]
    public async Task JsonParser_reads_object_and_array_forms()
    {
        var parser = new JsonParser();
        var objectWorkbook = await parser.ParseAsync(Text("{\"id\":1,\"name\":\"A\"}"), "one.json", Token.None);
        var arrayWorkbook = await parser.ParseAsync(Text("[{\"id\":1},{\"id\":2,\"active\":true}]"), "many.json", Token.None);

        Assert.AreEqual(2, objectWorkbook.Sheets[0].Columns.Count);
        Assert.AreEqual(2, arrayWorkbook.Sheets[0].Rows.Count);
        Assert.AreEqual("boolean", arrayWorkbook.Sheets[0].Rows[1].Cells[1].DataType);
        Assert.IsNull(arrayWorkbook.Sheets[0].Rows[0].Cells[1].Value);
    }

    [Test]
    public void JsonParser_rejects_primitive_root()
    {
        var exception = Assert.ThrowsAsync<InvalidDataException>(() =>
            new JsonParser().ParseAsync(Text("42"), "invalid.json", Token.None));
        StringAssert.Contains("object or an array", exception.Message);
    }

    [Test]
    public async Task XmlParser_reads_attributes_and_elements()
    {
        var workbook = await new XmlParser().ParseAsync(Text("<items><item id=\"1\"><name>A</name><qty>10</qty></item><item id=\"2\"><name>B</name></item></items>"), "items.xml", Token.None);
        Assert.That(workbook.Sheets, Has.Exactly(1).Items);
        var sheet = workbook.Sheets[0];
        CollectionAssert.Contains(sheet.Columns.Select(column => column.Name), "@id");
        Assert.AreEqual(2, sheet.Rows.Count);
        Assert.AreEqual("number", sheet.Rows[0].Cells.Single(cell => cell.Value == "10").DataType);
    }

    [Test]
    public async Task ExcelParser_reads_multiple_sheets_and_types()
    {
        using var stream = CreateWorkbook();
        var workbook = await new ExcelParser().ParseAsync(stream, "workbook.xlsx", Token.None);

        Assert.AreEqual(2, workbook.Sheets.Count);
        Assert.AreEqual("number", workbook.Sheets[0].Rows[0].Cells[1].DataType);
        Assert.AreEqual("boolean", workbook.Sheets[0].Rows[0].Cells[2].DataType);
        Assert.IsEmpty(workbook.Sheets[1].Rows);
    }

    [Test]
    public async Task ZipParser_parses_supported_entries_and_ignores_unknown_entries()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileParser, CsvParser>();
        services.AddSingleton<IFileParser, ZipParser>();
        using var provider = services.BuildServiceProvider();
        using var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var csv = zip.CreateEntry("items.csv").Open())
            {
                await Text("A,B\n1,2").CopyToAsync(csv);
            }
            using (var unknown = zip.CreateEntry("readme.bin").Open())
            {
                await Text("ignore me").CopyToAsync(unknown);
            }
        }

        archive.Position = 0;
        var parser = provider.GetRequiredService<IFileParser>();
        var workbook = await parser.ParseAsync(archive, "bundle.zip", Token.None);

        Assert.That(workbook.Sheets, Has.Exactly(1).Items);
        StringAssert.StartsWith("items - ", workbook.Sheets[0].Name);
    }

    [Test]
    public async Task ZipParser_rejects_archives_without_supported_documents()
    {
        using var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using var entry = zip.CreateEntry("readme.bin").Open();
            await Text("ignore me").CopyToAsync(entry);
        }
        archive.Position = 0;
        var services = new ServiceCollection().AddSingleton<IFileParser, ZipParser>().BuildServiceProvider();
        Assert.ThrowsAsync<InvalidDataException>(() =>
            services.GetRequiredService<IFileParser>().ParseAsync(archive, "empty.zip", Token.None));
    }

    private static MemoryStream Text(string value) => new(Encoding.UTF8.GetBytes(value));

    private static MemoryStream CreateWorkbook()
    {
        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("Data");
        var header = sheet.CreateRow(0);
        header.CreateCell(0).SetCellValue("Name");
        header.CreateCell(1).SetCellValue("Quantity");
        header.CreateCell(2).SetCellValue("Active");
        var row = sheet.CreateRow(1);
        row.CreateCell(0).SetCellValue("A");
        row.CreateCell(1).SetCellValue(12);
        row.CreateCell(2).SetCellValue(true);
        workbook.CreateSheet("Empty");
        var stream = new MemoryStream();
        workbook.Write(stream, leaveOpen: true);
        stream.Position = 0;
        return stream;
    }
}
