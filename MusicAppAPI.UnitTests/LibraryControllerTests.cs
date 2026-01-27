using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Moq;
using MusicAppAPI.Controllers;
using MusicAppAPI.Models;

namespace MusicAppAPI.UnitTests;

[TestFixture]
public class LibraryControllerTests
{
    private Mock<IMongoCollection<User>> _usersCollectionMock;
    private Mock<IMongoCollection<Track>> _tracksCollectionMock;
    private Mock<IMongoCollection<Album>> _albumsCollectionMock;
    private LibraryController _libraryController;
    private const string UserId = "user123";

    [SetUp]
    public void Setup()
    {
        _usersCollectionMock = new Mock<IMongoCollection<User>>();
        _tracksCollectionMock = new Mock<IMongoCollection<Track>>();
        _albumsCollectionMock = new Mock<IMongoCollection<Album>>();

        // The controller constructor takes collections directly, not the database
        _libraryController = new LibraryController(
            _usersCollectionMock.Object, 
            _albumsCollectionMock.Object, 
            _tracksCollectionMock.Object);

        // Mock HttpContext with User Claims
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim("id", UserId),
        }, "mock"));

        _libraryController.ControllerContext = new ControllerContext()
        {
            HttpContext = new DefaultHttpContext() { User = user }
        };
    }

    [Test]
    public async Task GetAllTracks_UserHasTracks_ReturnsTracksList()
    {
        // Arrange: User exists and has tracks in library
        var user = new User { Id = UserId, LibraryTracks = new List<string> { "track1", "track2" } };
        var tracks = new List<Track> 
        { 
            new Track { Id = "track1", Title = "Song A" },
            new Track { Id = "track2", Title = "Song B" }
        };

        // Mock finding user
        var userCursor = new Mock<IAsyncCursor<User>>();
        userCursor.Setup(c => c.Current).Returns(new List<User> { user });
        userCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        
        _usersCollectionMock.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<FindOptions<User, User>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCursor.Object);

        // Mock finding tracks
        var tracksCursor = new Mock<IAsyncCursor<Track>>();
        tracksCursor.Setup(c => c.Current).Returns(tracks);
        tracksCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);

        _tracksCollectionMock.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<Track>>(),
            It.IsAny<FindOptions<Track, Track>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(tracksCursor.Object);

        // Act
        var result = await _libraryController.GetAllTracks();

        // Assert
        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);
        var returnedTracks = okResult!.Value as List<Track>;
        Assert.That(returnedTracks, Has.Count.EqualTo(2));
        Assert.That(returnedTracks![0].Title, Is.EqualTo("Song A"));
    }

    [Test]
    public async Task AddTrack_NewTrack_ReturnsOk()
    {
        // Arrange: UpdateOneAsync returns MatchedCount = 1 (User found)
        var updateResult = new Mock<UpdateResult>();
        updateResult.Setup(r => r.MatchedCount).Returns(1);

        _usersCollectionMock.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(updateResult.Object);

        // Act
        var result = await _libraryController.AddTrack("newTrackId");

        // Assert
        var statusCodeResult = result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }

    [Test]
    public async Task RemoveTrack_TrackNotInLibrary_ReturnsBadRequest()
    {
        // Arrange: UpdateOneAsync returns MatchedCount = 1 (User found) but ModifiedCount = 0 (Track not removed)
        var updateResult = new Mock<UpdateResult>();
        updateResult.Setup(r => r.MatchedCount).Returns(1);
        updateResult.Setup(r => r.ModifiedCount).Returns(0);

        _usersCollectionMock.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(updateResult.Object);

        // Act
        var result = await _libraryController.RemoveTrack("nonExistentTrackId");

        // Assert
        var statusCodeResult = result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetAllAlbums_UserNotFound_ReturnsNotFound()
    {
        // Arrange: User not found in DB
        var userCursor = new Mock<IAsyncCursor<User>>();
        userCursor.Setup(c => c.Current).Returns(new List<User>()); // Empty list
        userCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        
        _usersCollectionMock.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<FindOptions<User, User>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCursor.Object);

        // Act
        var result = await _libraryController.GetAllAlbums();

        // Assert
        var statusCodeResult = result.Result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task AddAlbum_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange: Clear the user from HttpContext to simulate unauthenticated request
        _libraryController.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await _libraryController.AddAlbum("album1");

        // Assert
        var statusCodeResult = result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public void GetAllTracks_InternalError_ThrowsException()
    {
        // Arrange
        _usersCollectionMock.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<FindOptions<User, User>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Internal Error"));

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(async () => await _libraryController.GetAllTracks());
        Assert.That(ex!.Message, Is.EqualTo("Internal Error"));
    }
}