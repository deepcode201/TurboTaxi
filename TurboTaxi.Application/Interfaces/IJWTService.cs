using TurboTaxi.Domain.Entities;

namespace TurboTaxi.Application.Interfaces
{
    public interface IJWTService
    {
        string GenerateJwtToken(User user);
        string ValidateJwtToken(string token);
    }
}
