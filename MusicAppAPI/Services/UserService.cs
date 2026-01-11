using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using MusicAppAPI.Models;

namespace MusicAppAPI.Services;

public class UserService : IUserService
{
    private readonly IDynamoDBContext _context;
    private readonly IConfiguration _configuration;
    
    public UserService(IDynamoDBContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }
    
    public async Task<User?> RegisterAsync(string email, string password)
    {
        // Check if a user with the specified email already exists
        // Since email is not the primary key, we need to scan or query (if GSI exists).
        // For simplicity, we'll scan. In production, use GSI on Email.
        var conditions = new List<ScanCondition>
        {
            new ScanCondition("Email", ScanOperator.Equal, email)
        };
        var existingUsers = await _context.ScanAsync<User>(conditions).GetRemainingAsync();
        if (existingUsers.Count > 0) return null;
        
        User user = new()
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            // Replace the password with a hash generated for it
            Password = new PasswordHasher<object?>().HashPassword(null, password)
        };

        // The first ever user automatically becomes an admin
        // This is inefficient with DynamoDB scan, but keeping logic consistent.
        var allUsers = await _context.ScanAsync<User>(new List<ScanCondition>()).GetRemainingAsync();
        if (allUsers.Count == 0) user.Roles = ["admin"];
        
        await _context.SaveAsync(user);
        return user;
    }
    
    public async Task<User?> AuthenticateAsync(string email, string password)
    {
        // Check if a user with the specified email exists    
        var conditions = new List<ScanCondition>
        {
            new ScanCondition("Email", ScanOperator.Equal, email)
        };
        var users = await _context.ScanAsync<User>(conditions).GetRemainingAsync();
        User? user = users.FirstOrDefault();
        
        if (user == null) return null;
        
        // Check if the provided password is correct    
        PasswordVerificationResult passwordVerificationResult = new PasswordHasher<object?>().VerifyHashedPassword(null, user.Password!, password);
            
        // If the password is correct, add the JWT token to the user object
        if (passwordVerificationResult == PasswordVerificationResult.Failed) 
            return null;
        user.Token = CreateToken(user);
        user.Password = null;
        return user;
    }

    public async Task<bool> SetRoleAsync(string email, string role)
    {
        // Find a user with the specified email
        var conditions = new List<ScanCondition>
        {
            new ScanCondition("Email", ScanOperator.Equal, email)
        };
        var users = await _context.ScanAsync<User>(conditions).GetRemainingAsync();
        User? user = users.FirstOrDefault();
        
        if (user == null) return false;

        // Add the role only if it doesn't exist already (avoid duplicate roles)
        if (user.Roles == null) user.Roles = new List<string>();
        if (!user.Roles.Contains(role))
        {
            user.Roles.Add(role);
            await _context.SaveAsync(user);
        }
        
        return true;
    }
    
    private string CreateToken(User user)
    {
        JwtSecurityTokenHandler handler = new();
        
        byte[] privateKey = Encoding.UTF8.GetBytes(_configuration["JWT_KEY"] ?? "");
        SigningCredentials credentials = new(new SymmetricSecurityKey(privateKey), SecurityAlgorithms.HmacSha256Signature);
        
        SecurityTokenDescriptor tokenDescriptor = new()
        {
            SigningCredentials = credentials,
            Expires = DateTime.UtcNow.AddDays(1),
            Subject = GenerateClaims(user)
        };
          
        SecurityToken? token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }
    
    // Specifies what info will be contained within the token
    private static ClaimsIdentity GenerateClaims(User user)
    {
        ClaimsIdentity ci = new();
  
        ci.AddClaim(new Claim("id", user.Id!));
        if (user.Roles != null)
            foreach (string role in user.Roles)
                ci.AddClaim(new Claim(ClaimTypes.Role, role));
          
        return ci;
    }
}
