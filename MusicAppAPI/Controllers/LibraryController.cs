using System.Security.Claims;
using Amazon.DynamoDBv2.DataModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicAppAPI.Models;

namespace MusicAppAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LibraryController : ControllerBase
{
    private readonly IDynamoDBContext _context;
    private readonly ILogger<LibraryController> _logger;

    public LibraryController(IDynamoDBContext context, ILogger<LibraryController> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public class AlbumIdRequest
    {
        public string albumId { get; set; } = "";
    }
    
    public class TrackIdRequest
    {
        public string trackId { get; set; } = "";
    }
    
    [HttpGet("tracks")]
    public async Task<ActionResult> GetAllTracks()
    {
        try
        {
            // Get user id from the JWT token
            string? userId = HttpContext.User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId)) return StatusCode(StatusCodes.Status401Unauthorized);
            
            // Find the user in the database to get access to the user's library
            User? foundUser = await _context.LoadAsync<User>(userId);
            if (foundUser == null) return StatusCode(StatusCodes.Status404NotFound);
            
            // User documents only store tracks' ids, so fetch of the track collection is needed
            if (foundUser.LibraryTracks == null || foundUser.LibraryTracks.Count == 0)
                return Ok(new List<Track>());

            var trackBatch = _context.CreateBatchGet<Track>();
            foreach (var trackId in foundUser.LibraryTracks)
            {
                trackBatch.AddKey(trackId);
            }
            await trackBatch.ExecuteAsync();
            return Ok(trackBatch.Results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching library tracks");
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
    
    [HttpPatch("tracks")]
    public async Task<ActionResult> AddTrack([FromBody] string trackId)
    {
        try
        {
            // Get user id from the JWT token
            string? userId = HttpContext.User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId)) return StatusCode(StatusCodes.Status401Unauthorized);
            
            // Find the user in the database to get access to the user's library
            User? user = await _context.LoadAsync<User>(userId);
            if (user == null) return StatusCode(StatusCodes.Status401Unauthorized);
            
            // Add track to the user's library by id if it's not already added
            if (user.LibraryTracks == null) user.LibraryTracks = new List<string>();
            if (!user.LibraryTracks.Contains(trackId))
            {
                user.LibraryTracks.Add(trackId);
                await _context.SaveAsync(user);
            }
            
            _logger.LogInformation("Added track {TrackId} to user {UserId} library", trackId, userId);
            return StatusCode(StatusCodes.Status200OK);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding track {TrackId} to library", trackId);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
    
    [HttpDelete("tracks/{trackId}")]
    public async Task<ActionResult> RemoveTrack(string trackId)
    {
        try
        {
            // Get user id from the JWT token
            string? userId = HttpContext.User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId)) return StatusCode(StatusCodes.Status401Unauthorized);
            
            // Find the user in the database to get access to the user's library
            User? user = await _context.LoadAsync<User>(userId);
            if (user == null) return StatusCode(StatusCodes.Status401Unauthorized);
            
            // Remove track from the user's library by id
            if (user.LibraryTracks != null && user.LibraryTracks.Contains(trackId))
            {
                user.LibraryTracks.Remove(trackId);
                await _context.SaveAsync(user);
                _logger.LogInformation("Removed track {TrackId} from user {UserId} library", trackId, userId);
                return StatusCode(StatusCodes.Status200OK);
            }
            
            return StatusCode(StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing track {TrackId} from library", trackId);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
    
    [HttpGet("album")]
    public async Task<ActionResult<List<Album>>> GetAllAlbums()
    {
        try
        {
            // Get user id from the JWT token
            string? userId = HttpContext.User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId)) return StatusCode(StatusCodes.Status401Unauthorized);
            
            // Find the user in the database to get access to the user's library
            User? foundUser = await _context.LoadAsync<User>(userId);
            if (foundUser == null) return StatusCode(StatusCodes.Status404NotFound);
            
            // User documents only store albums' ids, so fetch of the album collection is needed
            if (foundUser.LibraryAlbums == null || foundUser.LibraryAlbums.Count == 0)
                return Ok(new List<Album>());

            var albumBatch = _context.CreateBatchGet<Album>();
            foreach (var albumId in foundUser.LibraryAlbums)
            {
                albumBatch.AddKey(albumId);
            }
            await albumBatch.ExecuteAsync();
            return Ok(albumBatch.Results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching library albums");
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
    
    [HttpPatch("albums")]
    public async Task<ActionResult> AddAlbum([FromBody] string albumId)
    {
        try
        {
            // Get user id from the JWT token
            string? userId = HttpContext.User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId)) return StatusCode(StatusCodes.Status401Unauthorized);
            
            // Find the user in the database to get the user's library
            User? user = await _context.LoadAsync<User>(userId);
            if (user == null) return StatusCode(StatusCodes.Status401Unauthorized);
            
            // Add album to the user's library by id if it's not already added
            if (user.LibraryAlbums == null) user.LibraryAlbums = new List<string>();
            if (!user.LibraryAlbums.Contains(albumId))
            {
                user.LibraryAlbums.Add(albumId);
                await _context.SaveAsync(user);
            }
            
            _logger.LogInformation("Added album {AlbumId} to user {UserId} library", albumId, userId);
            return StatusCode(StatusCodes.Status200OK);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding album {AlbumId} to library", albumId);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
    
    [HttpDelete("albums/{albumId}")]
    public async Task<ActionResult> RemoveAlbum(string albumId)
    {
        try
        {
            // Get user id from the JWT token
            string? userId = HttpContext.User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId)) return StatusCode(StatusCodes.Status401Unauthorized);
            
            // Find the user in the database to get the user's library
            User? user = await _context.LoadAsync<User>(userId);
            if (user == null) return StatusCode(StatusCodes.Status401Unauthorized);
            
            // Remove album from the user's library by id
            if (user.LibraryAlbums != null && user.LibraryAlbums.Contains(albumId))
            {
                user.LibraryAlbums.Remove(albumId);
                await _context.SaveAsync(user);
                _logger.LogInformation("Removed album {AlbumId} from user {UserId} library", albumId, userId);
                return StatusCode(StatusCodes.Status200OK);
            }
            
            return StatusCode(StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing album {AlbumId} from library", albumId);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
}
