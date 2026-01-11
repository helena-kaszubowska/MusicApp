using Amazon.DynamoDBv2.DataModel;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicAppAPI.Models;
using Newtonsoft.Json;

namespace MusicAppAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TracksController : ControllerBase
{
    private readonly IDynamoDBContext _context;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TracksController> _logger;

    public TracksController(IDynamoDBContext context, IAmazonSimpleNotificationService snsClient, IConfiguration configuration, ILogger<TracksController> logger)
    {
        _context = context;
        _snsClient = snsClient;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<Track>>> SearchTracks(string query)
    {
        try
        {
            // Scan all tracks and filter in memory (similar to AlbumsController)
            var conditions = new List<ScanCondition>();
            var allTracks = await _context.ScanAsync<Track>(conditions).GetRemainingAsync();
            
            var filteredTracks = allTracks.Where(t => 
                (t.Title != null && t.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) || 
                (t.Artist != null && t.Artist.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            return Ok(filteredTracks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tracks with query {Query}", query);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<Track>>> GetAllTracks()
    {
        try
        {
            var conditions = new List<ScanCondition>();
            var tracks = await _context.ScanAsync<Track>(conditions).GetRemainingAsync();
            return Ok(tracks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all tracks");
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [Authorize(Roles = "admin")]
    [HttpPost("{id}/upload")]
    public async Task<ActionResult> UploadFile(string id, IFormFile file)
    {
        try
        {
            if (file.Length == 0 || Path.GetExtension(file.FileName).ToLower() != ".flac")
                return StatusCode(StatusCodes.Status400BadRequest);

            string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "assets/audio");
            if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

            string fileName = id + ".flac";

            string filePath = Path.Combine(uploadPath, fileName);
            using (FileStream fs = new(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }
            
            _logger.LogInformation("Uploaded track file: {FileName}", fileName);
            return StatusCode(StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file for track {TrackId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpGet("{id}/download")]
    public ActionResult DownloadTrack(string id, [FromQuery] string? title, [FromQuery] string? artist)
    {
        try
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "assets/audio", id + ".flac");
            if (!System.IO.File.Exists(filePath)) return StatusCode(StatusCodes.Status404NotFound);
            
            // Adding title and artist in the query is optional but makes the file's name look appropriate
            string fileName = id + ".flac";
            if (artist != null && title != null)
                fileName = $"{artist} - {title}.flac";
            
            // Capture IP before starting background task
            string source = "Unknown";
            if (Request.HttpContext.Connection.RemoteIpAddress != null)
            {
                source = $"{Request.HttpContext.Connection.RemoteIpAddress}:{Request.HttpContext.Connection.RemotePort}";
            }

            // Send a message to SNS to track downloads
            Task.Run(async () =>
            {
                try
                {
                    TrackDownloadedMessage message = new()
                    {
                        TrackId = id,
                        TrackTitle = title,
                        TrackArtist = artist,
                        DownloadedAt = DateTime.UtcNow,
                        Source = source
                    };

                    var topicArn = _configuration["SNS:TopicArn"] ?? "arn:aws:sns:us-east-1:000000000000:music-app-topic";
                    var publishRequest = new PublishRequest
                    {
                        TopicArn = topicArn,
                        Message = JsonConvert.SerializeObject(message),
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                        {
                            { "MessageType", new MessageAttributeValue { DataType = "String", StringValue = "TrackDownloaded" } }
                        }
                    };
                    await _snsClient.PublishAsync(publishRequest);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending SNS message for track download");
                }
            });
            
            FileStream stream = new(filePath, FileMode.Open, FileAccess.Read);
            return File(stream, "audio/flac", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading track {TrackId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
}
