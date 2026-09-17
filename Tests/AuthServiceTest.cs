using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using SONDAGEAPI.Data;
using SONDAGEAPI.Models;
using SONDAGEAPI.Services;

namespace Tests;

/// <summary>
/// AuthService teste contre une vraie base SQLite en memoire, avec ITokenService
/// bouchonne. Volontairement pas le fournisseur InMemory d'EF Core : le hachage
/// BCrypt et les requetes sur RefreshTokens doivent s'executer pour de vrai.
/// </summary>
public class AuthServiceTest : IDisposable
{
    private const string Username = "fo";
    private const string Password = "SecurePassword123!";

    private readonly SqliteConnection connection;
    private readonly ApplicationDbContext db;
    private readonly Mock<ITokenService> mockTokenService;
    private readonly AuthService authService;

    public AuthServiceTest()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        mockTokenService = new Mock<ITokenService>();
        mockTokenService.Setup(service => service.GenerateJwtToken(It.IsAny<User>())).Returns("fake-jwt");
        mockTokenService.Setup(service => service.GenerateRefreshToken()).Returns("fake-refresh");
        mockTokenService.Setup(service => service.HashToken(It.IsAny<string>())).Returns("fake-refresh-hash");

        authService = new AuthService(db, mockTokenService.Object);
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
    }

    private async Task<User> SeedUserAsync(string username = Username, string password = Password)
    {
        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task RegisterAsync_ShouldReturnTrue_WhenTheUsernameIsFree()
    {
        // 1. Arrange (base vide)

        // 2. Act
        var result = await authService.RegisterAsync(Username, Password);

        // 3. Assert
        Assert.True(result);
        Assert.Equal(1, await db.Users.CountAsync(u => u.Username == Username));
    }

    [Fact]
    public async Task RegisterAsync_ShouldReturnFalse_WhenTheUsernameAlreadyExists()
    {
        // 1. Arrange
        await SeedUserAsync();

        // 2. Act
        var result = await authService.RegisterAsync(Username, "UnAutreMotDePasse456!");

        // 3. Assert
        Assert.False(result);
        Assert.Equal(1, await db.Users.CountAsync(u => u.Username == Username));
    }

    [Fact]
    public async Task RegisterAsync_ShouldNeverStoreThePasswordInClear()
    {
        // 1. Arrange & 2. Act
        await authService.RegisterAsync(Username, Password);

        // 3. Assert
        var stored = await db.Users.SingleAsync(u => u.Username == Username);

        Assert.NotEqual(Password, stored.PasswordHash);
        Assert.StartsWith("$2", stored.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(Password, stored.PasswordHash));
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData(Username, "")]
    [InlineData("", "")]
    public async Task LoginAsync_ShouldReturnNull_WhenCredentialsAreEmpty(string username, string password)
    {
        // 1. Arrange
        await SeedUserAsync();

        // 2. Act
        var result = await authService.LoginAsync(username, password);

        // 3. Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenTheUserDoesNotExist()
    {
        // 1. Arrange (base vide)

        // 2. Act
        var result = await authService.LoginAsync("inconnu", Password);

        // 3. Assert
        Assert.Null(result);
        mockTokenService.Verify(service => service.GenerateJwtToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenThePasswordIsWrong()
    {
        // 1. Arrange
        await SeedUserAsync();

        // 2. Act
        var result = await authService.LoginAsync(Username, "MauvaisMotDePasse!");

        // 3. Assert
        Assert.Null(result);
        mockTokenService.Verify(service => service.GenerateJwtToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnAnAccessToken_WhenCredentialsAreValid()
    {
        // 1. Arrange
        var user = await SeedUserAsync();

        // 2. Act
        var result = await authService.LoginAsync(Username, Password);

        // 3. Assert
        Assert.NotNull(result);
        Assert.Equal("fake-jwt", result.AccessToken);
        mockTokenService.Verify(service => service.GenerateJwtToken(It.Is<User>(u => u.Id == user.Id)), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldIssueARefreshToken_WhenTheUserHasNoLiveOne()
    {
        // 1. Arrange
        var user = await SeedUserAsync();

        // 2. Act
        await authService.LoginAsync(Username, Password);

        // 3. Assert
        var refreshToken = await db.RefreshTokens.SingleAsync(rt => rt.UserId == user.Id);

        Assert.Equal("fake-refresh-hash", refreshToken.TokenHash);
        Assert.True(refreshToken.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_ShouldStoreOnlyTheHashOfTheRefreshToken()
    {
        // 1. Arrange
        await SeedUserAsync();

        // 2. Act
        await authService.LoginAsync(Username, Password);

        // 3. Assert
        var refreshToken = await db.RefreshTokens.SingleAsync();

        Assert.NotEqual("fake-refresh", refreshToken.TokenHash);
        mockTokenService.Verify(service => service.HashToken("fake-refresh"), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldNotIssueASecondRefreshToken_WhenALiveOneExists()
    {
        // 1. Arrange
        var user = await SeedUserAsync();

        db.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = "jeton-encore-valide",
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();

        // 2. Act
        await authService.LoginAsync(Username, Password);

        // 3. Assert
        Assert.Equal(1, await db.RefreshTokens.CountAsync(rt => rt.UserId == user.Id));
        mockTokenService.Verify(service => service.GenerateRefreshToken(), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ShouldIssueANewRefreshToken_WhenTheExistingOneHasExpired()
    {
        // 1. Arrange
        var user = await SeedUserAsync();

        db.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = "jeton-perime",
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        // 2. Act
        await authService.LoginAsync(Username, Password);

        // 3. Assert
        Assert.Equal(2, await db.RefreshTokens.CountAsync(rt => rt.UserId == user.Id));
        mockTokenService.Verify(service => service.GenerateRefreshToken(), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldBeCaseSensitiveOnTheUsername()
    {
        // 1. Arrange
        await SeedUserAsync();

        // 2. Act
        var result = await authService.LoginAsync(Username.ToUpperInvariant(), Password);

        // 3. Assert
        Assert.Null(result);
    }
}
