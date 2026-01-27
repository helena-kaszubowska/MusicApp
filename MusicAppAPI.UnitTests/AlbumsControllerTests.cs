using EasyNetQ;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Moq;
using MusicAppAPI.Controllers;
using MusicAppAPI.Models;

namespace MusicAppAPI.UnitTests;

[TestFixture]
public class AlbumsControllerTests
{
    private Mock<IMongoCollection<Album>> _albumsCollectionMock;
    private Mock<IMongoCollection<Track>> _tracksCollectionMock;
    private Mock<IBus> _rabbitMQBusMock;
    private AlbumsController _albumsController;

    [SetUp]
    public void Setup()
    {
        _albumsCollectionMock = new Mock<IMongoCollection<Album>>();
        _tracksCollectionMock = new Mock<IMongoCollection<Track>>();
        _rabbitMQBusMock = new Mock<IBus>();
        
        _albumsController = new AlbumsController(
            _albumsCollectionMock.Object, 
            _tracksCollectionMock.Object, 
            _rabbitMQBusMock.Object);
    }

    [Test]
    public async Task AddNewAlbum_ValidAlbum_ReturnsCreated()
    {
        // Arrange
        var album = new Album
        {
            Title = "Test Album",
            Artist = "Test Artist",
            Year = 2023,
            Tracks = new List<Track>
            {
                new Track { Title = "Track 1", Length = 200, Nr = 1 }
            }
        };

        // Act
        var result = await _albumsController.AddNewAlbum(album);

        // Assert
        var createdResult = result as CreatedResult;
        Assert.That(createdResult, Is.Not.Null);
        Assert.That(createdResult!.StatusCode, Is.EqualTo(StatusCodes.Status201Created));
        
        // Verify tracks were inserted
        _tracksCollectionMock.Verify(c => c.InsertManyAsync(
            It.IsAny<IEnumerable<Track>>(),
            It.IsAny<InsertManyOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
            
        // Verify album was inserted
        _albumsCollectionMock.Verify(c => c.InsertOneAsync(
            It.IsAny<Album>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("Title", "Title must be specified.")]
    [TestCase("Artist", "Artist must be specified.")]
    [TestCase("Year", "Year must be specified.")]
    [TestCase("Tracks", "Album must have at least one track.")]
    public async Task AddNewAlbum_InvalidData_ReturnsBadRequest(string fieldToInvalidate, string expectedMessage)
    {
        // Arrange
        var album = new Album
        {
            Title = fieldToInvalidate == "Title" ? null : "Test Album",
            Artist = fieldToInvalidate == "Artist" ? null : "Test Artist",
            Year = fieldToInvalidate == "Year" ? null : 2025,
            Tracks = fieldToInvalidate == "Tracks" ? [] : [new Track { Title = "T1", Length = 200 }]
        };

        // Act
        var result = await _albumsController.AddNewAlbum(album);

        // Assert
        var objectResult = result as ObjectResult;
        Assert.That(objectResult, Is.Not.Null);
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(objectResult.Value, Is.EqualTo(expectedMessage));
    }

    [Test]
    public async Task DeleteAlbum_AlbumExists_ReturnsOk()
    {
        // Arrange
        string albumId = "album1";
        string trackId = "track1";
        var album = new Album { Id = albumId, TrackIds = new List<string> { trackId } };
        
        // Mock Find to return the album (needed to get track IDs)
        var mockCursor = new Mock<IAsyncCursor<Album>>();
        mockCursor.Setup(c => c.Current).Returns(new List<Album> { album });
        mockCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        
        _albumsCollectionMock.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<Album>>(),
            It.IsAny<FindOptions<Album, Album>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        // Mock DeleteOneAsync
        var deleteResult = new Mock<DeleteResult>();
        deleteResult.Setup(r => r.DeletedCount).Returns(1);
        
        _albumsCollectionMock.Setup(c => c.DeleteOneAsync(
            It.IsAny<FilterDefinition<Album>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleteResult.Object);

        // Create dummy file to prevent DirectoryNotFoundException in the controller
        string assetsPath = Path.Combine(Directory.GetCurrentDirectory(), "assets", "audio");
        Directory.CreateDirectory(assetsPath);
        string filePath = Path.Combine(assetsPath, trackId + ".flac");
        await File.WriteAllTextAsync(filePath, "dummy content");

        try
        {
            // Act
            var result = await _albumsController.DeleteAlbum(albumId);

            // Assert
            var statusCodeResult = result as StatusCodeResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            
            // Verify tracks were deleted
            _tracksCollectionMock.Verify(c => c.DeleteManyAsync(
                It.IsAny<FilterDefinition<Track>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            // Cleanup
            if (File.Exists(filePath)) File.Delete(filePath);
            try 
            {
                if (Directory.Exists(assetsPath)) Directory.Delete(assetsPath);
                string assetsParent = Path.Combine(Directory.GetCurrentDirectory(), "assets");
                if (Directory.Exists(assetsParent)) Directory.Delete(assetsParent);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }

    [Test]
    public async Task DeleteAlbum_AlbumNotFound_ReturnsNotFound()
    {
        // Arrange
        const string albumId = "unknown";
        
        // Mock Find to return null
        var mockCursor = new Mock<IAsyncCursor<Album>>();
        mockCursor.Setup(c => c.Current).Returns(new List<Album>());
        mockCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>())).Returns(false);
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        
        _albumsCollectionMock.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<Album>>(),
            It.IsAny<FindOptions<Album, Album>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        // Mock DeleteOneAsync
        var deleteResult = new Mock<DeleteResult>();
        deleteResult.Setup(r => r.DeletedCount).Returns(0);
        
        _albumsCollectionMock.Setup(c => c.DeleteOneAsync(
            It.IsAny<FilterDefinition<Album>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleteResult.Object);

        // Act
        var result = await _albumsController.DeleteAlbum(albumId);

        // Assert
        var statusCodeResult = result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }
}