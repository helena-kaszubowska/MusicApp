using MusicAppAnalytics.Services;
using MusicAppAPI.Models;

namespace MusicAppAnalytics.UnitTests;

[TestFixture]
public class AlbumAnalyticsServiceTests
{
    private AlbumAnalyticsService _service;

    [SetUp]
    public void Setup()
    {
        _service = new AlbumAnalyticsService();
    }

    [Test]
    public void RecordView_NewAlbum_AddsStats()
    {
        // Arrange
        var message = new AlbumViewedMessage
        {
            AlbumId = "album1",
            AlbumTitle = "Test Album",
            AlbumArtist = "Test Artist",
            ViewedAt = DateTime.UtcNow,
            Source = "127.0.0.1"
        };

        // Act
        _service.RecordView(message);

        // Assert
        var stats = _service.GetTopAlbums().FirstOrDefault();
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats!.AlbumId, Is.EqualTo(message.AlbumId));
        Assert.That(stats.ViewCount, Is.EqualTo(1));
        Assert.That(stats.Sources, Contains.Item(message.Source));
    }

    [Test]
    public void RecordView_ExistingAlbum_IncrementsViewCount()
    {
        // Arrange
        var message1 = new AlbumViewedMessage
        {
            AlbumId = "album1",
            AlbumTitle = "Test Album",
            AlbumArtist = "Test Artist",
            ViewedAt = DateTime.UtcNow,
            Source = "127.0.0.1"
        };
        
        var message2 = new AlbumViewedMessage
        {
            AlbumId = "album1",
            AlbumTitle = "Test Album",
            AlbumArtist = "Test Artist",
            ViewedAt = DateTime.UtcNow.AddMinutes(1),
            Source = "192.168.0.1"
        };

        // Act
        _service.RecordView(message1);
        _service.RecordView(message2);

        // Assert
        var stats = _service.GetTopAlbums().FirstOrDefault();
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats!.ViewCount, Is.EqualTo(2));
        Assert.That(stats.Sources, Has.Count.EqualTo(2));
        Assert.That(stats.LastViewedAt, Is.EqualTo(message2.ViewedAt));
    }

    [Test]
    public void GetTopAlbums_ReturnsOrderedByViewCount()
    {
        // Arrange
        var popularAlbum = new AlbumViewedMessage
        {
            AlbumId = "pop",
            AlbumTitle = "Popular",
            AlbumArtist = "Artist",
            ViewedAt = DateTime.UtcNow,
            Source = "src"
        };
        
        var lessPopularAlbum = new AlbumViewedMessage
        {
            AlbumId = "less",
            AlbumTitle = "Less Popular",
            AlbumArtist = "Artist",
            ViewedAt = DateTime.UtcNow,
            Source = "src"
        };

        // Act
        // Popular album viewed twice
        _service.RecordView(popularAlbum);
        _service.RecordView(popularAlbum);
        
        // Less popular viewed once
        _service.RecordView(lessPopularAlbum);

        var topAlbums = _service.GetTopAlbums().ToList();

        // Assert
        Assert.That(topAlbums, Has.Count.EqualTo(2));
        Assert.That(topAlbums[0].AlbumId, Is.EqualTo("pop"));
        Assert.That(topAlbums[0].ViewCount, Is.EqualTo(2));
        Assert.That(topAlbums[1].AlbumId, Is.EqualTo("less"));
        Assert.That(topAlbums[1].ViewCount, Is.EqualTo(1));
    }

    [Test]
    public void RecordView_ConcurrentAccess_HandlesThreadSafety()
    {
        // Arrange
        var message = new AlbumViewedMessage
        {
            AlbumId = "concurrent",
            AlbumTitle = "Concurrent Album",
            AlbumArtist = "Artist",
            ViewedAt = DateTime.UtcNow,
            Source = "src"
        };
        
        int numberOfThreads = 100;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < numberOfThreads; i++)
        {
            tasks.Add(Task.Run(() => _service.RecordView(message)));
        }
        
        Task.WaitAll(tasks.ToArray());

        // Assert
        var stats = _service.GetTopAlbums().FirstOrDefault();
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats!.ViewCount, Is.EqualTo(numberOfThreads));
    }
}