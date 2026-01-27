using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MusicAppAPI.Models;
using MusicAppAPI.Services;

namespace MusicAppAPI.Controllers;

[ApiController]
[Route("api")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }
    
    [HttpPost("sign-up")]
    public async Task<ActionResult> SignUpAsync([FromBody] SignData signData)
    {
        return StatusCode(await _userService.RegisterAsync(signData.Email, signData.Password) == null 
            ? StatusCodes.Status400BadRequest : StatusCodes.Status201Created);
    }
    
    [HttpPost("sign-in")]
    public async Task<ActionResult<User>> SignInAsync([FromBody] SignData signData)
    {
        User? authenticatedUser = await _userService.AuthenticateAsync(signData.Email, signData.Password);
        return authenticatedUser == null ? StatusCode(StatusCodes.Status401Unauthorized) : Ok(authenticatedUser);
    }

    [Authorize(Roles = "admin")]
    [HttpPatch("[controller]/give-admin-rights")]
    public async Task<ActionResult> GiveAdminRightsAsync([FromBody] string email)
    {
        return await _userService.SetRoleAsync(email, "admin")
            ? StatusCode(StatusCodes.Status200OK)
            : StatusCode(StatusCodes.Status400BadRequest);
    }

    [Authorize]
    [HttpDelete("users/{id}")]
    public async Task<ActionResult> DeleteUserAsync(string id)
    {
        string? currentUserId = User.FindFirst("id")?.Value;
        bool isAdmin = User.IsInRole("admin");

        // Allow deletion if user is admin OR if user is deleting their own account
        if (!isAdmin && currentUserId != id)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await _userService.DeleteUserAsync(id)
            ? StatusCode(StatusCodes.Status200OK)
            : StatusCode(StatusCodes.Status404NotFound);
    }
    
    public class SignData
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}