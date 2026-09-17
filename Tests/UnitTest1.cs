using Microsoft.AspNetCore.Mvc;
using Moq;
using SONDAGEAPI;
using SONDAGEAPI.Controllers;
using SONDAGEAPI.Models;
using SONDAGEAPI.Services;

namespace Tests;

public class AuthControllerTest
{
    private Mock<IAuthService> mockAuthService;
    
    public AuthControllerTest()
    {
        mockAuthService = new Mock<IAuthService>();
    }
    
    [Fact]
    public async Task InvalidCredentialsTest()
    {
        // 1. Arrange
        var request = new LoginRequest
        {
            Username = "testuser",
            Password = "SecurePassword123!"
        };
        
        mockAuthService
            .Setup(service => service.LoginAsync(request.Username, request.Password))
            .ReturnsAsync((LoginResponse?)null);
        
        var authController = new AuthController(mockAuthService.Object);

        // 2. Act
        var result = await authController.Login(request);
        
        // 3. Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        
        Assert.Equal(401, unauthorizedResult.StatusCode);
        Assert.Equal("Invalid username or password.", unauthorizedResult.Value);
    }
    
    [Fact]
    public async Task ValidCredentialsTest()
    {
        // 1. Arrange
        var request = new SONDAGEAPI.Models.LoginRequest
        {
            Username = "validuser",
            Password = "ValidPassword123!"
        };

        var expectedResponse = new SONDAGEAPI.Models.LoginResponse
        {
            AccessToken = "fake-valid-access-token",
        };

        mockAuthService
            .Setup(service => service.LoginAsync(request.Username, request.Password))
            .ReturnsAsync(expectedResponse);

        var authController = new AuthController(mockAuthService.Object);

        // 2. Act
        var result = await authController.Login(request);

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var loginResponse = Assert.IsType<SONDAGEAPI.Models.LoginResponse>(okResult.Value);

        Assert.Equal(expectedResponse.AccessToken, loginResponse.AccessToken);
    }
    
    [Fact]
    public async Task RegisterAsync_ShouldReturnFalse_WhenUsernameAlreadyExists()
    {
        // 1. Arrange
        string username = "existinguser";
        string password = "SecurePassword123!";

        mockAuthService
            .Setup(service => service.RegisterAsync(username, password))
            .ReturnsAsync(false);

        // 2. Act
        var result = await mockAuthService.Object.RegisterAsync(username, password);

        // 3. Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task RegisterAsync_ShouldReturnTrue_WhenRegistrationIsSuccessful()
    {
        // 1. Arrange
        string username = "newuser";
        string password = "SecurePassword123!";

        mockAuthService
            .Setup(service => service.RegisterAsync(username, password))
            .ReturnsAsync(true);

        // 2. Act
        var result = await mockAuthService.Object.RegisterAsync(username, password);

        // 3. Assert
        Assert.True(result);
    }
}
