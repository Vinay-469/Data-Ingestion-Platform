using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Interfaces;

public interface IFileValidationService
{
    void Validate(IFormFile file);
}
