using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using MusicAppAPI.Models;

namespace MusicAppAPI.Services;

public class UserService : IUserService
{
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IPasswordHasher<User> _passwordHasher;
    
    public UserService(IMongoCollection<User> usersCollection, IPasswordHasher<User> passwordHasher)
    {
        _usersCollection = usersCollection;
        _passwordHasher = passwordHasher;
    }
    
    public async Task<User?> RegisterAsync(string email, string password)
    {
        // Check if a user with the specified email already exists    
        FilterDefinition<User>? filter = Builders<User>.Filter.Eq(u => u.Email, email);
        if (await _usersCollection.CountDocumentsAsync(filter) > 0) return null;
        
        User user = new()
        {
            Email = email,
            // Replace the password with a hash generated for it
            Password = _passwordHasher.HashPassword(null!, password)
        };

        // The first ever user automatically becomes an admin
        long count = await _usersCollection.CountDocumentsAsync(_ => true);
        if (count == 0) user.Roles = ["admin"];
        
        await _usersCollection.InsertOneAsync(user);
        return user;
    }
    
    public async Task<User?> AuthenticateAsync(string email, string password)
    {
        // Check if a user with the specified email exists    
        FilterDefinition<User>? filter = Builders<User>.Filter.Eq(u => u.Email, email);
        
        var user = await _usersCollection.Find(filter).FirstOrDefaultAsync();
        
        if (user == null) return null;
        
        // Check if the provided password is correct    
        PasswordVerificationResult passwordVerificationResult = _passwordHasher.VerifyHashedPassword(null!, user.Password!, password);
            
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
        FilterDefinition<User>? filter = Builders<User>.Filter.Eq(u => u.Email, email);
        // Add the role only if it doesn't exist already (avoid duplicate roles)
        UpdateDefinition<User>? update = Builders<User>.Update.AddToSet(user => user.Roles, role);
        // If the user is found, the role has been assigned either previously or now
        return (await _usersCollection.UpdateOneAsync(filter, update)).MatchedCount > 0;
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        FilterDefinition<User> filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        DeleteResult result = await _usersCollection.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }
    
    private string CreateToken(User user)
    {
        JwtSecurityTokenHandler handler = new();
        
        byte[] privateKey = Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY")!); // See launchSettings.json
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