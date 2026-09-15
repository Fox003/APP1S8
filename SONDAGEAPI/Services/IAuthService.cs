using SONDAGEAPI.Models;

namespace SONDAGEAPI.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(string username, string password);
    Task<bool> RegisterAsync(string username, string password);
}