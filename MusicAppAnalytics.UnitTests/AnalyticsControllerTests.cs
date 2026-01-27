using Microsoft.AspNetCore.Mvc;
using MusicAppAnalytics.Controllers;
using MusicAppAnalytics.Services;
using MusicAppAPI.Models;

namespace MusicAppAnalytics.UnitTests;

[TestFixture]
public class AnalyticsControllerTests
{
    private AlbumAnalyticsService _albumService;
    private TrackAnalyticsService _trackService;
    private AnalyticsController _controller;

    [SetUp]
    public void Setup()
    {
        // Since services are simple in-memory stores, we can use real instances instead of mocks.
        // This is often better for "stateful" services where mocking internal dictionary logic is tedious.
        _albumService = new AlbumAnalyticsService();
        _trackService = new TrackAnalyticsService();
        _controller = new AnalyticsController(_albumService, _trackService);
    }

    [Test]
    public void GetTopAlbums_ReturnsOkWithData()
    {
        // Arrange
        _albumService.RecordView(new AlbumViewedMessage 
        { 
            AlbumId = "a1", AlbumTitle = "T1", AlbumArtist = "A1", ViewedAt = DateTime.UtcNow, Source = "s" 
        });

        // Act
        var result = _controller.GetTopAlbums(5);

        // Assert
        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);
        var albums = okResult!.Value as IEnumerable<AlbumStats>;
        Assert.That(albums!.Count(), Is.EqualTo(1));
        Assert.That(albums!.First().AlbumId, Is.EqualTo("a1"));
    }

    [Test]
    public void GetTopTracks_NoArtist_ReturnsAllTopTracks()
    {
        // Arrange
        _trackService.RecordDownload(new TrackDownloadedMessage 
        { 
            TrackId = "t1", TrackTitle = "T1", TrackArtist = "A1", DownloadedAt = DateTime.UtcNow, Source = "s" 
        });
        _trackService.RecordDownload(new TrackDownloadedMessage 
        { 
            TrackId = "t2", TrackTitle = "T2", TrackArtist = "A2", DownloadedAt = DateTime.UtcNow, Source = "s" 
        });

        // Act
        var result = _controller.GetTopTracks(null, 10);

        // Assert
        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);
        var tracks = okResult!.Value as IEnumerable<TrackStats>;
        Assert.That(tracks!.Count(), Is.EqualTo(2));
    }

    [Test]
    public void GetTopTracks_WithArtist_ReturnsFilteredTracks()
    {
        // Arrange
        _trackService.RecordDownload(new TrackDownloadedMessage 
        { 
            TrackId = "t1", TrackTitle = "T1", TrackArtist = "TargetArtist", DownloadedAt = DateTime.UtcNow, Source = "s" 
        });
        _trackService.RecordDownload(new TrackDownloadedMessage 
        { 
            TrackId = "t2", TrackTitle = "T2", TrackArtist = "OtherArtist", DownloadedAt = DateTime.UtcNow, Source = "s" 
        });

        // Act
        var result = _controller.GetTopTracks("TargetArtist", 10);

        // Assert
        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);
        var tracks = okResult!.Value as IEnumerable<TrackStats>;
        Assert.That(tracks!.Count(), Is.EqualTo(1));
        Assert.That(tracks!.First().TrackArtist, Is.EqualTo("TargetArtist"));
    }
}