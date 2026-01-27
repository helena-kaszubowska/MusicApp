using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MusicAppAPI.Models;

namespace MusicAppAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LibraryController : ControllerBase
{
    private IMongoCollection<User> _usersCollection;
    private IMongoCollection<Album> _albumsCollection;
    private IMongoCollection<Track> _tracksCollection;
    

    public LibraryController(IMongoCollection<User> usersCollection, IMongoCollection<Album> albumsCollection, IMongoCollection<Track> tracksCollection)
    {
        _usersCollection = usersCollection;
        _albumsCollection = albumsCollection;
        _tracksCollection = tracksCollection;
    }
    
    [HttpGet("tracks")]
    public async Task<ActionResult> GetAllTracks()
    {
        // Get user id from the JWT token
        string? userId = HttpContext.User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userId)) return StatusCode(StatusCodes.Status401Unauthorized);
        
        // Find the user in the database to get access to the user's library
        FilterDefinition<User>? userFilter = Builders<User>.Filter.Eq(u => u.Id, userId);
        User? foundUser = await _usersCollection.Find(userFilter).FirstOrDefaultAsync();
        if (foundUser == null) return StatusCode(StatusCodes.Status404NotFound);
        
        // User documents only store tracks' ids, so fetch of the track collection is needed
        FilterDefinition<Track>? trackFilter = Builders<Track>.Filter.In(t => t.Id, foundUser.LibraryTracks);
        return Ok(await _tracksCollection.Find(trackFilter).ToListAsync());
    }
    
    [HttpPatch("tracks")]
    public async Task<ActionResult> AddTrack([FromBody] string trackId)
    {
        // Get user id from the JWT token
        string? userId = HttpContext.User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userId)) return StatusCode(StatusCodes.Status401Unauthorized);
        
        // Find the user in the database to get access to the user's library
        FilterDefinition<User>? filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        
        // Add track to the user's library by id if it's not already added
        UpdateDefinition<User>? updateDefinition = Builders<User>.Update.AddToSet(u => u.LibraryTracks, trackId);
        UpdateResult? result = await _usersCollection.UpdateOneAsync(filter, updateDefinition);
        return result.MatchedCount == 0 ? StatusCode(StatusCodes.Status401Unauthorized) : StatusCode(StatusCodes.Status200OK);
    }
    
    [HttpDelete("tracks/{trackId}")]
    public async Task<ActionResult> RemoveTrack(string trackId)
    {
        // Get user id from the JWT token
        string? userId = HttpContext.User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userId)) return StatusCode(StatusCodes.Status401Unauthorized);
        
        // Find the user in the database to get access to the user's library
        FilterDefinition<User>? filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        
        // Remove track from the user's library by id
        UpdateDefinition<User>? updateDefinition = Builders<User>.Update.Pull(u => u.LibraryTracks, trackId);
        UpdateResult? result = await _usersCollection.UpdateOneAsync(filter, updateDefinition);
        return result.MatchedCount == 0 ? StatusCode(StatusCodes.Status401Unauthorized) : 
            result.ModifiedCount == 0 ? StatusCode(StatusCodes.Status400BadRequest) : 
            StatusCode(StatusCodes.Status200OK);
    }
    
    [HttpGet("album")]
    public async Task<ActionResult<List<Album>>> GetAllAlbums()
    {
        // Get user id from the JWT token
        string? userId = HttpContext.User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userId)) return StatusCode(StatusCodes.Status401Unauthorized);
        
        // Find the user in the database to get access to the user's library
        FilterDefinition<User>? userFilter = Builders<User>.Filter.Eq(u => u.Id, userId);
        User? foundUser = await _usersCollection.Find(userFilter).FirstOrDefaultAsync();
        if (foundUser == null) return StatusCode(StatusCodes.Status404NotFound);
        
        // User documents only store albums' ids, so fetch of the album collection is needed
        FilterDefinition<Album>? albumFilter = Builders<Album>.Filter.In(a => a.Id, foundUser.LibraryAlbums);
        
        // When requesting all the albums, tracks are usually not needed and create too much boilerplate
        ProjectionDefinition<Album>? projection = Builders<Album>.Projection.Exclude("tracks");
        return Ok(await _albumsCollection.Find(albumFilter).Project<Album>(projection).ToListAsync());
    }
    
    [HttpPatch("albums")]
    public async Task<ActionResult> AddAlbum([FromBody] string albumId)
    {
        // Get user id from the JWT token
        string? userId = HttpContext.User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userId)) return StatusCode(StatusCodes.Status401Unauthorized);
        
        // Find the user in the database to get the user's library
        FilterDefinition<User>? filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        
        // Add album to the user's library by id if it's not already added
        UpdateDefinition<User>? updateDefinition = Builders<User>.Update.AddToSet(u => u.LibraryAlbums, albumId);
        UpdateResult? result = await _usersCollection.UpdateOneAsync(filter, updateDefinition);
        return result.MatchedCount == 0 ? StatusCode(StatusCodes.Status401Unauthorized) 
            : StatusCode(StatusCodes.Status200OK);
    }
    
    [HttpDelete("albums/{albumId}")]
    public async Task<ActionResult> RemoveAlbum(string albumId)
    {
        // Get user id from the JWT token
        string? userId = HttpContext.User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userId)) return StatusCode(StatusCodes.Status401Unauthorized);
        
        // Find the user in the database to get the user's library
        FilterDefinition<User>? filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        
        // Remove album from the user's library by id
        UpdateDefinition<User>? updateDefinition = Builders<User>.Update.Pull(u => u.LibraryAlbums, albumId);
        UpdateResult? result = await _usersCollection.UpdateOneAsync(filter, updateDefinition);
        return result.MatchedCount == 0 ? StatusCode(StatusCodes.Status401Unauthorized) : 
            result.ModifiedCount == 0 ? StatusCode(StatusCodes.Status400BadRequest) : 
            StatusCode(StatusCodes.Status200OK);
    }
}
