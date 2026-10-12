using CoreModels.DTOs;

namespace ApplicationLayer.Interfaces;

public interface IFileParser
{
    bool CanParse(string extension);
    Task<ParsedWorkbook> ParseAsync(Stream stream, string fileName, Token token);
}
