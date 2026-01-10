using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicAppAPI.Models;
using Newtonsoft.Json;

namespace MusicAppAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlbumsController : ControllerBase
{
    private readonly IDynamoDBContext _context;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IConfiguration _configuration;

    public AlbumsController(IDynamoDBContext context, IAmazonSimpleNotificationService snsClient, IConfiguration configuration)
    {
        _context = context;
        _snsClient = snsClient;
        _configuration = configuration;
    }

    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<ActionResult> AddNewAlbum([FromBody] Album album)
    {
        try
        {
            if (album.Title is null) return StatusCode(StatusCodes.Status400BadRequest, "Title must be specified.");
            if (album.Artist is null) return StatusCode(StatusCodes.Status400BadRequest, "Artist must be specified.");
            if (album.Year is null) return StatusCode(StatusCodes.Status400BadRequest, "Year must be specified.");
            if (album.Tracks == null || album.Tracks.Count == 0)
                return StatusCode(StatusCodes.Status400BadRequest, "Album must have at least one track.");

            // Validate tracks
            if (album.Tracks.Any(t => t.Title == null || t.Length == null))
                return StatusCode(StatusCodes.Status400BadRequest, "Some tracks are missing title and/or length.");
            if (album.Tracks.Any(t => t.Nr == null))
                for (int i = 0; i < album.Tracks.Count; i++)
                    album.Tracks[i].Nr = i + 1;

            // Generate an id if it's not provided
            album.Id ??= Guid.NewGuid().ToString();

            foreach (Track track in album.Tracks)
            {
                track.Id ??= Guid.NewGuid().ToString();
                track.Artist ??= album.Artist;
                track.AlbumTitle = album.Title;
                track.AlbumId = album.Id;
                track.Year ??= album.Year;
            }

            // Store tracks in a separate collection
            var trackBatch = _context.CreateBatchWrite<Track>();
            trackBatch.AddPutItems(album.Tracks);
            await trackBatch.ExecuteAsync();

            // Only track ids are stored with the album in the database
            album.TrackIds = album.Tracks.Select(t => t.Id!).ToList();
            album.Tracks = null;

            await _context.SaveAsync(album);
            return Created($"api/albums/{album.Id}", null);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<Album>>> GetAllAlbums()
    {
        try
        {
            // When requesting all the albums, tracks are usually not needed and create too much boilerplate
            var conditions = new List<ScanCondition>();
            var albums = await _context.ScanAsync<Album>(conditions).GetRemainingAsync();
            return Ok(albums);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateAlbum(string id, [FromBody] Album albumUpdate)
    {
        try
        {
            if (albumUpdate.Title is null) return StatusCode(StatusCodes.Status400BadRequest, "Title must be specified.");
            if (albumUpdate.Artist is null) return StatusCode(StatusCodes.Status400BadRequest, "Artist must be specified.");
            if (albumUpdate.Year is null) return StatusCode(StatusCodes.Status400BadRequest, "Year must be specified.");
            if (albumUpdate.Tracks == null || albumUpdate.Tracks.Count == 0)
                return StatusCode(StatusCodes.Status400BadRequest, "Album must have at least one track.");
            
            // Fetch existing album to get trackIds
            Album? album = await _context.LoadAsync<Album>(id);
            if (album == null) return StatusCode(StatusCodes.Status404NotFound);

            // Validate tracks
            if (albumUpdate.Tracks!.Any(t => t.Title == null || t.Length == null))
                return StatusCode(StatusCodes.Status400BadRequest, "Some tracks are missing title and/or length.");
            if (albumUpdate.Tracks!.Any(t => t.Nr == null))
                for (int i = 0; i < albumUpdate.Tracks!.Count; i++)
                    albumUpdate.Tracks[i].Nr = i + 1;
            foreach (Track track in albumUpdate.Tracks!)
            {
                track.Artist ??= album.Artist;
                track.AlbumTitle = album.Title;
                track.AlbumId = id;
                track.Year ??= album.Year;
            }

            // Compare tracks to identify removals
            List<string> existingTrackIds = album.TrackIds ?? new List<string>();
            List<string> updatedTrackIds = albumUpdate.Tracks.Where(t => t.Id != null).Select(t => t.Id!).ToList();
            List<string> tracksToRemove = existingTrackIds.Except(updatedTrackIds).ToList();

            var trackBatch = _context.CreateBatchWrite<Track>();

            // New tracks (id is null)
            List<Track> newTracks = albumUpdate.Tracks.Where(t => t.Id is null).ToList();
            foreach (Track newTrack in newTracks)
            {
                newTrack.Id = Guid.NewGuid().ToString();
                trackBatch.AddPutItem(newTrack);
            }

            // Existing tracks (id is non-null)
            List<Track> existingTracks = albumUpdate.Tracks.Where(t => t.Id != null).ToList();
            foreach (Track track in existingTracks)
                trackBatch.AddPutItem(track);

            // Tracks to remove
            foreach (string trackId in tracksToRemove)
            {
                trackBatch.AddDeleteItem(new Track { Id = trackId });
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "assets/audio", trackId + ".flac");
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            await trackBatch.ExecuteAsync();
            
            // Only track ids are stored with the album in the database
            albumUpdate.Id = id;
            albumUpdate.TrackIds = albumUpdate.Tracks.Select(t => t.Id!).ToList();
            albumUpdate.Tracks = null;
            
            await _context.SaveAsync(albumUpdate);

            return StatusCode(StatusCodes.Status200OK);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAlbum(string id)
    {
        try
        {
            Album? album = await _context.LoadAsync<Album>(id);
            if (album == null) return StatusCode(StatusCodes.Status404NotFound);

            await _context.DeleteAsync(album);

            // Remove all tracks of the album as well
            if (album.TrackIds != null && album.TrackIds.Count > 0)
            {
                var trackBatch = _context.CreateBatchWrite<Track>();
                foreach (string trackId in album.TrackIds)
                {
                    trackBatch.AddDeleteItem(new Track { Id = trackId });
                    string filePath = Path.Combine(Directory.GetCurrentDirectory(), "assets/audio", trackId + ".flac");
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }
                await trackBatch.ExecuteAsync();
            }

            return StatusCode(StatusCodes.Status200OK);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Album>> GetAlbumById(string id)
    {
        try
        {
            Album? album = await _context.LoadAsync<Album>(id);

            if (album is null) return StatusCode(StatusCodes.Status404NotFound);

            // Fetch tracks
            if (album.TrackIds != null && album.TrackIds.Count > 0)
            {
                var trackBatch = _context.CreateBatchGet<Track>();
                foreach (var trackId in album.TrackIds)
                {
                    trackBatch.AddKey(trackId);
                }
                await trackBatch.ExecuteAsync();
                album.Tracks = trackBatch.Results;
            }
            else
            {
                album.Tracks = new List<Track>();
            }

            // These fields are useless in the context of an album
            foreach (Track track in album.Tracks!)
            {
                track.AlbumTitle = null;
                track.AlbumId = null;
            }

            // Send a message to SNS to track album views
            Task.Run(async () =>
            {
                try
                {
                    AlbumViewedMessage message = new()
                    {
                        AlbumId = album.Id!,
                        AlbumTitle = album.Title!,
                        AlbumArtist = album.Artist!,
                        ViewedAt = DateTime.UtcNow,
                        Source =
                            $"{Request.HttpContext.Connection.RemoteIpAddress}:{Request.HttpContext.Connection.RemotePort}"
                    };

                    var topicArn = _configuration["SNS:TopicArn"] ?? "arn:aws:sns:us-east-1:000000000000:music-app-topic";
                    var publishRequest = new PublishRequest
                    {
                        TopicArn = topicArn,
                        Message = JsonConvert.SerializeObject(message),
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                        {
                            { "MessageType", new MessageAttributeValue { DataType = "String", StringValue = "AlbumViewed" } }
                        }
                    };
                    await _snsClient.PublishAsync(publishRequest);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
            
            return Ok(album);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<Album>>> SearchAlbums(string query)
    {
        try
        {
            var allAlbums = await _context.ScanAsync<Album>(new List<ScanCondition>()).GetRemainingAsync();
            
            var filteredAlbums = allAlbums.Where(a => 
                (a.Title != null && a.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) || 
                (a.Artist != null && a.Artist.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            return Ok(filteredAlbums);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
}
