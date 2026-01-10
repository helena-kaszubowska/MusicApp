using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicAppAPI.Models;
using MusicAppAPI.Services;

namespace MusicAppAPI.Controllers;

[ApiController]
[Route("api")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserService userService, ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }
    
    [HttpPost("sign-up")]
    public async Task<ActionResult> SignUpAsync([FromBody] SignData signData)
    {
        try
        {
            var user = await _userService.RegisterAsync(signData.Email, signData.Password);
            if (user == null)
            {
                _logger.LogWarning("Failed sign-up attempt for email: {Email}", signData.Email);
                return StatusCode(StatusCodes.Status400BadRequest);
            }
            
            _logger.LogInformation("User signed up successfully: {Email}", signData.Email);
            return StatusCode(StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sign-up for email {Email}", signData.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
    
    [HttpPost("sign-in")]
    public async Task<ActionResult<User>> SignInAsync([FromBody] SignData signData)
    {
        try
        {
            User? authenticatedUser = await _userService.AuthenticateAsync(signData.Email, signData.Password);
            if (authenticatedUser == null)
            {
                _logger.LogWarning("Failed sign-in attempt for email: {Email}", signData.Email);
                return StatusCode(StatusCodes.Status401Unauthorized);
            }
            
            _logger.LogInformation("User signed in successfully: {Email}", signData.Email);
            return Ok(authenticatedUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sign-in for email {Email}", signData.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [Authorize(Roles = "admin")]
    [HttpPatch("[controller]/give-admin-rights")]
    public async Task<ActionResult> GiveAdminRightsAsync([FromBody] string email)
    {
        try
        {
            var result = await _userService.SetRoleAsync(email, "admin");
            if (result)
            {
                _logger.LogInformation("Admin rights granted to user: {Email}", email);
                return StatusCode(StatusCodes.Status200OK);
            }
            
            _logger.LogWarning("Failed to grant admin rights to user: {Email}", email);
            return StatusCode(StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting admin rights to user {Email}", email);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
    
    public class SignData
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}
