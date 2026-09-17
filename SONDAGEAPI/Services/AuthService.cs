using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Data;
using SONDAGEAPI.Models;

namespace SONDAGEAPI.Services;

public class AuthService(ApplicationDbContext db, ITokenService tokenService) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        var accessToken = tokenService.GenerateJwtToken(user);
        
        var hasLiveRefreshToken = await db.RefreshTokens
            .AnyAsync(rt => rt.UserId == user.Id && rt.ExpiresAt > DateTime.UtcNow);

        string? refreshToken = null;

        if (!hasLiveRefreshToken)
        {
            refreshToken = tokenService.GenerateRefreshToken();

            db.RefreshTokens.Add(new RefreshToken
            {
                TokenHash = tokenService.HashToken(refreshToken),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });

            await db.SaveChangesAsync();
        }

        return new LoginResponse
        {
            AccessToken = accessToken,
        };
    }

    public async Task<bool> RegisterAsync(string username, string password)
    {
        if (await db.Users.AnyAsync(u => u.Username == username))
        {
            return false;
        }
        
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        
        var user = new User
        {
            Username = username,
            PasswordHash = passwordHash
        };

        db.Users.Add(user);
        return await db.SaveChangesAsync() > 0;
    }
}