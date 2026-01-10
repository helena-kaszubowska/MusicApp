using Microsoft.AspNetCore.Mvc;
using MusicAppAnalytics.Services;

namespace MusicAppAnalytics.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AlbumAnalyticsService _albumAnalyticsService;
    private readonly TrackAnalyticsService _trackAnalyticsService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(AlbumAnalyticsService albumAnalyticsService, TrackAnalyticsService trackAnalyticsService, ILogger<AnalyticsController> logger)
    {
        _albumAnalyticsService = albumAnalyticsService;
        _trackAnalyticsService = trackAnalyticsService;
        _logger = logger;
    }

    [HttpGet("top-albums")]
    public ActionResult GetTopAlbums([FromQuery] int count = 10)
    {
        _logger.LogInformation("Fetching top {Count} albums", count);
        return Ok(_albumAnalyticsService.GetTopAlbums(count));
    }
    
    [HttpGet("top-tracks")]
    public ActionResult GetTopTracks([FromQuery] string? artist, [FromQuery] int count = 10)
    {
        if (artist != null)
        {
            _logger.LogInformation("Fetching top {Count} tracks for artist {Artist}", count, artist);
        }
        else
        {
            _logger.LogInformation("Fetching top {Count} tracks", count);
        }
        
        return Ok(artist is null 
            ? _trackAnalyticsService.GetTopTracks(count) 
            : _trackAnalyticsService.GetTopTracksByArtist(artist, count));
    }
}
