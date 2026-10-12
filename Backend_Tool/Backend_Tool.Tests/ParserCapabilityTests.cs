using BusinessLayer.Parsers;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Backend_Tool.Tests;

public sealed class ParserCapabilityTests
{
    [Test]
    [TestCase(".xlsx")]
    [TestCase(".xls")]
    public void ExcelParser_supports_excel_extensions(string extension) => Assert.IsTrue(new ExcelParser().CanParse(extension));

    [Test]
    [TestCase(".csv")]
    [TestCase(".tsv")]
    [TestCase(".txt")]
    public void Text_parsers_support_their_extensions(string extension)
    {
        Assert.IsTrue(extension == ".csv" ? new CsvParser().CanParse(extension) : new DelimitedTextParser().CanParse(extension));
    }

    [Test]
    [TestCase(".json")]
    [TestCase(".xml")]
    [TestCase(".pdf")]
    [TestCase(".docx")]
    [TestCase(".zip")]
    public void Document_parsers_support_expected_extensions(string extension)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        ApplicationLayer.Interfaces.IFileParser parser = extension switch
        {
            ".json" => new JsonParser(),
            ".xml" => new XmlParser(),
            ".pdf" => new PdfParser(),
            ".docx" => new DocParser(),
            ".zip" => new ZipParser(services),
            _ => throw new InvalidOperationException()
        };
        Assert.IsTrue(parser.CanParse(extension));
        Assert.IsFalse(parser.CanParse(".unsupported"));
    }
}
