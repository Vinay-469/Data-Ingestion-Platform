using System.IO.Compression;
using ApplicationLayer.Interfaces;
using CoreModels.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessLayer.Parsers;

public sealed class ZipParser(IServiceProvider services) : IFileParser
{
    public bool CanParse(string extension) => extension == ".zip";

    public async Task<ParsedWorkbook> ParseAsync(Stream stream, string fileName, Token token)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var parsers = services.GetServices<IFileParser>().Where(parser => parser is not ZipParser).ToList();
        var sheets = new List<ParsedSheet>();
        foreach (var entry in archive.Entries.Where(entry => !string.IsNullOrWhiteSpace(entry.Name)))
        {
            token.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(entry.Name).ToLowerInvariant();
            var parser = parsers.FirstOrDefault(candidate => candidate.CanParse(extension));
            if (parser is null) continue;
            await using var entryStream = entry.Open();
            var parsed = await parser.ParseAsync(entryStream, entry.Name, token);
            foreach (var sheet in parsed.Sheets)
            {
                sheets.Add(sheet with { Name = $"{Path.GetFileNameWithoutExtension(entry.Name)} - {sheet.Name}", Order = sheets.Count });
            }
        }

        if (sheets.Count == 0) throw new InvalidDataException("The ZIP file does not contain a supported data document.");
        return new ParsedWorkbook(sheets);
    }
}
