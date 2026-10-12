using Microsoft.AspNetCore.Http;
using BusinessLayer.Services;
using NUnit.Framework;

namespace Backend_Tool.Tests;

public sealed class FileValidationServiceTests
{
    private readonly FileValidationService service = new();

    [Test]
    [TestCase("data.xlsx")]
    [TestCase("data.XLS")]
    [TestCase("data.csv")]
    [TestCase("data.tsv")]
    [TestCase("data.txt")]
    [TestCase("data.json")]
    [TestCase("data.xml")]
    [TestCase("data.pdf")]
    [TestCase("data.docx")]
    [TestCase("data.zip")]
    public void Validate_accepts_supported_extensions(string fileName)
    {
        service.Validate(CreateFile(fileName, "content"));
    }

    [Test]
    public void Validate_rejects_empty_file()
    {
        var exception = Assert.Throws<InvalidDataException>(() => service.Validate(CreateFile("empty.csv", string.Empty)));
        Assert.AreEqual("The file is empty.", exception.Message);
    }

    [Test]
    public void Validate_rejects_unsupported_extension()
    {
        var exception = Assert.Throws<InvalidDataException>(() => service.Validate(CreateFile("data.exe", "content")));
        StringAssert.Contains("Supported formats", exception.Message);
    }

    [Test]
    public void Validate_rejects_files_over_100_mb()
    {
        var file = new FormFile(new MemoryStream(new byte[1]), 0, 100_000_001, "file", "large.csv");
        var exception = Assert.Throws<InvalidDataException>(() => service.Validate(file));
        Assert.AreEqual("The maximum file size is 100 MB.", exception.Message);
    }

    private static IFormFile CreateFile(string name, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);
    }
}
