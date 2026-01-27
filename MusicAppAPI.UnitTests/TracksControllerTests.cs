using System.Text;
using EasyNetQ;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Moq;
using MusicAppAPI.Controllers;
using MusicAppAPI.Models;

namespace MusicAppAPI.UnitTests;

[TestFixture]
public class TracksControllerTests
{
    private Mock<IMongoCollection<Track>> _tracksCollectionMock;
    private Mock<IBus> _rabbitMQBusMock;
    private Mock<IPubSub> _pubSubMock;
    private TracksController _tracksController;

    [SetUp]
    public void Setup()
    {
        _tracksCollectionMock = new Mock<IMongoCollection<Track>>();
        _rabbitMQBusMock = new Mock<IBus>();
        _pubSubMock = new Mock<IPubSub>();

        // Setup RabbitMQ Bus to return the PubSub mock
        _rabbitMQBusMock.Setup(b => b.PubSub).Returns(_pubSubMock.Object);

        _tracksController = new TracksController(_tracksCollectionMock.Object, _rabbitMQBusMock.Object);
        
        // Setup default HttpContext for tests that need it
        _tracksController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Test]
    public async Task SearchTracks_QueryMatchesTitle_ReturnsMatchingTracks()
    {
        // Arrange
        const string query = "Test";
        var tracks = new List<Track> 
        { 
            new Track { Title = "Test", Artist = "Artist" },
            new Track { Title = "Boring Test", Artist = "Singer" }
        };

        var cursorMock = new Mock<IAsyncCursor<Track>>();
        cursorMock.Setup(c => c.Current).Returns(tracks);
        cursorMock.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        _tracksCollectionMock.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<Track>>(),
            It.IsAny<FindOptions<Track, Track>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _tracksController.SearchTracks(query);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Has.Count.EqualTo(2));
        // Verify that FindAsync was called
        _tracksCollectionMock.Verify(c => c.FindAsync(
            It.IsAny<FilterDefinition<Track>>(),
            It.IsAny<FindOptions<Track, Track>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UploadFile_ValidFlacFile_ReturnsCreatedAndSavesFile()
    {
        // Arrange
        const string trackId = "track123";
        const string content = "fake audio content";
        const string fileName = "song.flac";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(stream.Length);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((s, t) => stream.CopyTo(s))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _tracksController.UploadFile(trackId, fileMock.Object);

        // Assert
        var statusCodeResult = result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status201Created));

        // Verify file existence and cleanup
        string expectedPath = Path.Combine(Directory.GetCurrentDirectory(), "assets/audio", trackId + ".flac");
        try
        {
            Assert.That(File.Exists(expectedPath), Is.True, "File should have been created");
        }
        finally
        {
            if (File.Exists(expectedPath)) File.Delete(expectedPath);
        }
    }

    [Test]
    public async Task UploadFile_InvalidExtension_ReturnsBadRequest()
    {
        // Arrange
        const string trackId = "track123";
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("song.mp3"); // Wrong extension
        fileMock.Setup(f => f.Length).Returns(100);

        // Act
        var result = await _tracksController.UploadFile(trackId, fileMock.Object);

        // Assert
        var statusCodeResult = result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public void DownloadTrack_FileExists_ReturnsFileResult()
    {
        // Arrange
        const string trackId = "download_test";
        string assetsPath = Path.Combine(Directory.GetCurrentDirectory(), "assets", "audio");
        string filePath = Path.Combine(assetsPath, trackId + ".flac");
        
        Directory.CreateDirectory(assetsPath);
        File.WriteAllText(filePath, "dummy content");

        Stream? streamToDispose = null;

        try
        {
            // Act
            var result = _tracksController.DownloadTrack(trackId, "Song Title", "Artist Name");

            // Assert
            var fileResult = result as FileStreamResult;
            Assert.That(fileResult, Is.Not.Null);
            Assert.That(fileResult!.ContentType, Is.EqualTo("audio/flac"));
            Assert.That(fileResult.FileDownloadName, Is.EqualTo("Artist Name - Song Title.flac"));
            
            // Capture the stream to dispose it later
            streamToDispose = fileResult.FileStream;
        }
        finally
        {
            // Cleanup
            // IMPORTANT: Dispose the stream BEFORE trying to delete the file
            streamToDispose?.Dispose();
            
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Test]
    public void DownloadTrack_FileDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string trackId = "non_existent";

        // Act
        var result = _tracksController.DownloadTrack(trackId, null, null);

        // Assert
        var statusCodeResult = result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }
}