using SONDAGEAPI.Models;

namespace SONDAGEAPI.Services;

public interface ITokenService
{
    string GenerateJwtToken(User user);
    string GenerateRefreshToken();
    string HashToken(string token);
}