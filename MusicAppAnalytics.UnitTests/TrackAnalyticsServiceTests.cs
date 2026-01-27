using MusicAppAnalytics.Services;
using MusicAppAPI.Models;

namespace MusicAppAnalytics.UnitTests;

[TestFixture]
public class TrackAnalyticsServiceTests
{
    private TrackAnalyticsService _service;

    [SetUp]
    public void Setup()
    {
        _service = new TrackAnalyticsService();
    }

    [Test]
    public void RecordDownload_NewTrack_AddsStats()
    {
        // Arrange
        var message = new TrackDownloadedMessage
        {
            TrackId = "track1",
            TrackTitle = "Test Track",
            TrackArtist = "Test Artist",
            DownloadedAt = DateTime.UtcNow,
            Source = "127.0.0.1"
        };

        // Act
        _service.RecordDownload(message);

        // Assert
        var stats = _service.GetTopTracks().FirstOrDefault();
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats!.TrackId, Is.EqualTo(message.TrackId));
        Assert.That(stats.DownloadCount, Is.EqualTo(1));
        Assert.That(stats.TrackTitle, Is.EqualTo(message.TrackTitle));
    }

    [Test]
    public void RecordDownload_UpdatesMetadata_WhenProvided()
    {
        // Arrange
        var initialMessage = new TrackDownloadedMessage
        {
            TrackId = "track1",
            TrackTitle = null, // Missing metadata initially
            TrackArtist = null,
            DownloadedAt = DateTime.UtcNow,
            Source = "src1"
        };

        var updateMessage = new TrackDownloadedMessage
        {
            TrackId = "track1",
            TrackTitle = "Updated Title", // Metadata provided later
            TrackArtist = "Updated Artist",
            DownloadedAt = DateTime.UtcNow,
            Source = "src2"
        };

        // Act
        _service.RecordDownload(initialMessage);
        _service.RecordDownload(updateMessage);

        // Assert
        var stats = _service.GetTopTracks().First();
        Assert.That(stats.TrackTitle, Is.EqualTo("Updated Title"));
        Assert.That(stats.TrackArtist, Is.EqualTo("Updated Artist"));
        Assert.That(stats.DownloadCount, Is.EqualTo(2));
    }

    [Test]
    public void GetTopTracksByArtist_FiltersCorrectly()
    {
        // Arrange
        var artist1Track = new TrackDownloadedMessage
        {
            TrackId = "t1",
            TrackTitle = "Song 1",
            TrackArtist = "Artist A",
            DownloadedAt = DateTime.UtcNow,
            Source = "src"
        };
        
        var artist2Track = new TrackDownloadedMessage
        {
            TrackId = "t2",
            TrackTitle = "Song 2",
            TrackArtist = "Artist B",
            DownloadedAt = DateTime.UtcNow,
            Source = "src"
        };

        // Act
        _service.RecordDownload(artist1Track);
        _service.RecordDownload(artist1Track); // 2 downloads
        _service.RecordDownload(artist2Track); // 1 download

        var artistAStats = _service.GetTopTracksByArtist("Artist A").ToList();
        var artistBStats = _service.GetTopTracksByArtist("Artist B").ToList();

        // Assert
        Assert.That(artistAStats, Has.Count.EqualTo(1));
        Assert.That(artistAStats[0].TrackId, Is.EqualTo("t1"));
        
        Assert.That(artistBStats, Has.Count.EqualTo(1));
        Assert.That(artistBStats[0].TrackId, Is.EqualTo("t2"));
    }

    [Test]
    public void RecordDownload_ConcurrentAccess_HandlesThreadSafety()
    {
        // Arrange
        var message = new TrackDownloadedMessage
        {
            TrackId = "concurrent_track",
            TrackTitle = "Title",
            TrackArtist = "Artist",
            DownloadedAt = DateTime.UtcNow,
            Source = "src"
        };
        
        int numberOfThreads = 100;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < numberOfThreads; i++)
        {
            tasks.Add(Task.Run(() => _service.RecordDownload(message)));
        }
        
        Task.WaitAll(tasks.ToArray());

        // Assert
        var stats = _service.GetTopTracks().FirstOrDefault();
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats!.DownloadCount, Is.EqualTo(numberOfThreads));
    }
}