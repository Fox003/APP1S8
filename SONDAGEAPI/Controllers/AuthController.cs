using Microsoft.AspNetCore.Mvc;
using SONDAGEAPI.Models;
using SONDAGEAPI.Services;

namespace SONDAGEAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request.Username, request.Password);

        return result is not null
            ? Ok(result)
            : Unauthorized("Invalid username or password.");
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register(LoginRequest request)
    {
        var success = await authService.RegisterAsync(request.Username, request.Password);

        if (!success)
            return BadRequest("Username already exists or registration failed.");

        return Ok("User registered successfully!");
    }
}