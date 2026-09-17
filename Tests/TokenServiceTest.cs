using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Moq;
using SONDAGEAPI.Models;
using SONDAGEAPI.Services;

namespace Tests;

/// <summary>
/// TokenService, teste pour de vrai (pas de bouchon) : seule la configuration
/// est bouchonnee avec Moq. Un jeton mal forme ou un jeton previsible est une
/// faille, donc les proprietes de securite sont verifiees explicitement.
/// </summary>
public class TokenServiceTest
{
    // 32 octets minimum, sinon HmacSha256 refuse la cle.
    private const string JwtKey = "cle-de-test-suffisamment-longue-pour-hmac-sha256";
    private const string JwtIssuer = "SONDAGEAPI.Tests";
    private const string JwtAudience = "SONDAGEAPI.Tests.Audience";

    private Mock<IConfiguration> mockConfiguration;
    private TokenService tokenService;

    public TokenServiceTest()
    {
        mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(config => config["Jwt:Key"]).Returns(JwtKey);
        mockConfiguration.Setup(config => config["Jwt:Issuer"]).Returns(JwtIssuer);
        mockConfiguration.Setup(config => config["Jwt:Audience"]).Returns(JwtAudience);

        tokenService = new TokenService(mockConfiguration.Object);
    }

    private static User CreateUser() => new()
    {
        Id = 7,
        Username = "fo",
        PasswordHash = "peu-importe",
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public void GenerateJwtToken_ShouldCarryTheUserIdentity()
    {
        // 1. Arrange
        var user = CreateUser();

        // 2. Act
        var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenService.GenerateJwtToken(user));

        // 3. Assert
        Assert.Equal(user.Id.ToString(), token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Username, token.Claims.First(c => c.Type == ClaimTypes.Name).Value);
    }

    [Fact]
    public void GenerateJwtToken_ShouldUseTheConfiguredIssuerAndAudience()
    {
        // 1. Arrange
        var user = CreateUser();

        // 2. Act
        var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenService.GenerateJwtToken(user));

        // 3. Assert
        Assert.Equal(JwtIssuer, token.Issuer);
        Assert.Contains(JwtAudience, token.Audiences);
    }

    [Fact]
    public void GenerateJwtToken_ShouldNeverContainThePasswordHash()
    {
        // 1. Arrange
        var user = CreateUser();
        user.PasswordHash = "$2a$11$hash-qui-ne-doit-jamais-sortir";

        // 2. Act
        var rawToken = tokenService.GenerateJwtToken(user);

        // 3. Assert
        Assert.DoesNotContain("hash-qui-ne-doit-jamais-sortir", rawToken);
    }

    [Fact]
    public void GenerateJwtToken_ShouldExpireWithinFifteenMinutes()
    {
        // 1. Arrange
        var user = CreateUser();
        var before = DateTime.UtcNow;

        // 2. Act
        var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenService.GenerateJwtToken(user));

        // 3. Assert
        Assert.True(token.ValidTo > before);
        Assert.True(token.ValidTo <= before.AddMinutes(15).AddSeconds(5));
    }

    [Fact]
    public void GenerateJwtToken_ShouldGiveEachTokenItsOwnJti()
    {
        // 1. Arrange
        var user = CreateUser();
        var handler = new JwtSecurityTokenHandler();

        // 2. Act
        var first = handler.ReadJwtToken(tokenService.GenerateJwtToken(user));
        var second = handler.ReadJwtToken(tokenService.GenerateJwtToken(user));

        // 3. Assert
        Assert.NotEqual(
            first.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value,
            second.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnADistinctValueEveryTime()
    {
        // 1. Arrange
        int sampleSize = 100;

        // 2. Act
        var tokens = Enumerable.Range(0, sampleSize)
            .Select(_ => tokenService.GenerateRefreshToken())
            .ToList();

        // 3. Assert
        Assert.Equal(sampleSize, tokens.Distinct().Count());
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnSixtyFourBytesOfEntropy()
    {
        // 1. Arrange & 2. Act
        var token = tokenService.GenerateRefreshToken();

        // 3. Assert
        Assert.Equal(64, Convert.FromBase64String(token).Length);
    }

    [Fact]
    public void HashToken_ShouldBeDeterministic()
    {
        // 1. Arrange
        var token = tokenService.GenerateRefreshToken();

        // 2. Act
        var first = tokenService.HashToken(token);
        var second = tokenService.HashToken(token);

        // 3. Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void HashToken_ShouldNotRevealTheToken()
    {
        // 1. Arrange
        var token = tokenService.GenerateRefreshToken();

        // 2. Act
        var hash = tokenService.HashToken(token);

        // 3. Assert
        Assert.NotEqual(token, hash);
        Assert.Equal(32, Convert.FromBase64String(hash).Length); // SHA-256
    }

    [Fact]
    public void HashToken_ShouldProduceDifferentHashesForDifferentTokens()
    {
        // 1. Arrange
        var first = tokenService.GenerateRefreshToken();
        var second = tokenService.GenerateRefreshToken();

        // 2. Act
        var firstHash = tokenService.HashToken(first);
        var secondHash = tokenService.HashToken(second);

        // 3. Assert
        Assert.NotEqual(firstHash, secondHash);
    }
}
