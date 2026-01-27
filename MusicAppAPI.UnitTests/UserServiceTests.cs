using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using Moq;
using MusicAppAPI.Models;
using MusicAppAPI.Services;

namespace MusicAppAPI.UnitTests;

[TestFixture]
public class UserServiceTests
{
    private Mock<IMongoCollection<User>> _collectionMock;
    private Mock<IAsyncCursor<User>> _cursorMock;
    private Mock<IPasswordHasher<User>> _passwordHasherMock;
    private UserService _userService;
    
    [SetUp]
    public void Setup()
    {
        _collectionMock = new Mock<IMongoCollection<User>>();
        _cursorMock = new Mock<IAsyncCursor<User>>();
        _passwordHasherMock = new Mock<IPasswordHasher<User>>();
        
        // We need to set the JWT_KEY environment variable for token generation tests
        Environment.SetEnvironmentVariable("JWT_KEY", "super_secret_key_for_testing_purposes_only_12345");
        
        _userService = new UserService(_collectionMock.Object, _passwordHasherMock.Object);
    }

    [TearDown]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("JWT_KEY", null);
    }

    [Test]
    public async Task RegisterAsync_FirstUser_InsertsUserWithAdminRole()
    {
        // Arrange
        const string email = "first@example.com";
        const string password = "pass123";
        const string hash = "hash";
        
        _passwordHasherMock.Setup(x => x.HashPassword(It.IsAny<User>(), password))
            .Returns(hash);
        
        // Setup CountDocumentsAsync to return 0 (indicating no registered users)
        _collectionMock.Setup(c => c.CountDocumentsAsync(
                It.IsAny<FilterDefinition<User>>()))
            .ReturnsAsync(0);

        // Act
        var user = await _userService.RegisterAsync(email, password);

        // Assert
        Assert.That(user, Is.Not.Null);
        Assert.That(user.Email, Is.EqualTo(email));
        Assert.That(user.Roles, Does.Contain("admin"));
        Assert.That(user.Password, Is.EqualTo(hash));
        
        // Verify that InsertOneAsync was called once
        _collectionMock.Verify(c => c.InsertOneAsync(
            It.IsAny<User>()), Times.Once);
    }

    [Test]
    public async Task RegisterAsync_SubsequentUserWithUniqueEmail_InsertsUserWithoutAdminRole()
    {
        const string email = "user@example.com";
        const string password = "pass123";
        const string hash = "hash";
        
        _passwordHasherMock.Setup(x => x.HashPassword(It.IsAny<User>(), password))
            .Returns(hash);
        
        // Setup sequence:
        // 1. Return 0 to indicate no existing users with the same email
        // 2. Return 1 to indicate an existing user
        _collectionMock.SetupSequence(c => c.CountDocumentsAsync(
            It.IsAny<FilterDefinition<User>>()))
            .ReturnsAsync(0)
            .ReturnsAsync(1);
        
        var user = await _userService.RegisterAsync(email, password);
        
        Assert.That(user, Is.Not.Null);
        Assert.That(user.Roles, Is.Null.Or.Empty);
        Assert.That(user.Password, Is.EqualTo(hash));
        
        _collectionMock.Verify(c => c.InsertOneAsync(
            It.IsAny<User>()), Times.Once);
    }

    [Test]
    public async Task RegisterAsync_EmailExists_ReturnsNull()
    {
        const string email = "existing@example.com";
        
        // Setup CountDocumentsAsync to return 1 (indicating user with the same email exists)
        _collectionMock.Setup(c => c.CountDocumentsAsync(
            It.IsAny<FilterDefinition<User>>()))
            .ReturnsAsync(1);

        // Act
        var user = await _userService.RegisterAsync(email, "password");

        // Assert
        Assert.That(user, Is.Null);
        
        // Verify that InsertOneAsync wasn't called
        _collectionMock.Verify(c => c.InsertOneAsync(
            It.IsAny<User>()), Times.Never);
    }

    [Test]
    public async Task AuthenticateAsync_UserNotFound_ReturnsNull()
    {
        _cursorMock.Setup(c => c.Current).Returns(new List<User>());
        _cursorMock.Setup(c => c.MoveNextAsync()).ReturnsAsync(false);

        _collectionMock.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<FindOptions<User, User>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cursorMock.Object);
        
        var result = await _userService.AuthenticateAsync("unknown@example.com", "password");
        
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task AuthenticateAsync_WrongPassword_ReturnsNull()
    {
        // Arrange
        const string password = "wrongPassword";
        const string storedHash = "hashed_correct_password";
        var user = new User
        {
            Email = "test@example.com",
            Password = storedHash
        };

        // Mock FindAsync to return the user
        _cursorMock.Setup(c => c.Current).Returns(new List<User> { user });
        _cursorMock.SetupSequence(c => c.MoveNextAsync()).ReturnsAsync(true).ReturnsAsync(false);

        _collectionMock.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<FindOptions<User, User>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cursorMock.Object);

        // Mock PasswordHasher to fail verification
        _passwordHasherMock.Setup(x => x.VerifyHashedPassword(null!, storedHash, password))
            .Returns(PasswordVerificationResult.Failed);

        // Act
        var result = await _userService.AuthenticateAsync("test@example.com", password);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task AuthenticateAsync_CorrectCredentials_ReturnsUserWithToken()
    {
        // Arrange
        const string password = "correctPassword";
        const string storedHash = "hashed_correct_password";
        var user = new User
        {
            Id = "userId",
            Email = "test@example.com",
            Password = storedHash
        };

        // Mock FindAsync to return the user
        _cursorMock.Setup(c => c.Current).Returns(new List<User> { user });
        _cursorMock.SetupSequence(c => c.MoveNextAsync()).ReturnsAsync(true).ReturnsAsync(false);

        _collectionMock.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<FindOptions<User, User>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cursorMock.Object);

        // Mock PasswordHasher to succeed verification
        _passwordHasherMock.Setup(x => x.VerifyHashedPassword(null!, storedHash, password))
            .Returns(PasswordVerificationResult.Success);

        // Act
        var result = await _userService.AuthenticateAsync("test@example.com", password);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Token, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Password, Is.Null); // Password should be cleared
    }

    [Test]
    public async Task SetRoleAsync_UserExists_ReturnsTrue()
    {
        // Arrange
        var updateResultMock = new Mock<UpdateResult>();
        updateResultMock.Setup(r => r.MatchedCount).Returns(1);
        updateResultMock.Setup(r => r.IsAcknowledged).Returns(true);

        _collectionMock.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>()))
            .ReturnsAsync(updateResultMock.Object);

        // Act
        var result = await _userService.SetRoleAsync("test@example.com", "admin");

        // Assert
        Assert.That(result, Is.True);
        
        _collectionMock.Verify(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>()), Times.Once);
    }

    [Test]
    public async Task SetRoleAsync_UserDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var updateResultMock = new Mock<UpdateResult>();
        updateResultMock.Setup(r => r.MatchedCount).Returns(0);
        updateResultMock.Setup(r => r.IsAcknowledged).Returns(true);

        _collectionMock.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>()))
            .ReturnsAsync(updateResultMock.Object);

        // Act
        var result = await _userService.SetRoleAsync("unknown@example.com", "admin");

        // Assert
        Assert.That(result, Is.False);
    }
}