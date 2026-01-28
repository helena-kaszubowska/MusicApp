using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MusicAppAPI.Controllers;
using MusicAppAPI.Models;
using MusicAppAPI.Services;

namespace MusicAppAPI.UnitTests;

[TestFixture]
public class UserControllerTests
{
    private Mock<IUserService> _userServiceMock;
    private UserController _userController;
    
    [SetUp]
    public void Setup()
    {
        _userServiceMock = new Mock<IUserService>();
        _userController = new UserController(_userServiceMock.Object);
    }

    // --- SignUpAsync Tests ---

    [Test]
    public async Task SignUpAsync_ValidData_ReturnsCreated()
    {
        // Arrange
        var signData = new UserController.SignData { Email = "test@example.com", Password = "password" };
        var user = new User { Email = signData.Email };
        
        _userServiceMock.Setup(s => s.RegisterAsync(signData.Email, signData.Password))
            .ReturnsAsync(user);

        // Act
        var result = await _userController.SignUpAsync(signData);

        // Assert
        var statusCodeResult = result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status201Created));
    }

    [Test]
    public async Task SignUpAsync_UserAlreadyExists_ReturnsBadRequest()
    {
        // Arrange
        var signData = new UserController.SignData { Email = "existing@example.com", Password = "password" };
        
        // Return null to simulate failure (e.g., user exists)
        _userServiceMock.Setup(s => s.RegisterAsync(signData.Email, signData.Password))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _userController.SignUpAsync(signData);

        // Assert
        var statusCodeResult = result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public void SignUpAsync_InternalError_ThrowsException()
    {
        // Arrange
        var signData = new UserController.SignData { Email = "error@example.com", Password = "password" };
        
        _userServiceMock.Setup(s => s.RegisterAsync(signData.Email, signData.Password))
            .ThrowsAsync(new Exception("Internal error"));

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(async () => await _userController.SignUpAsync(signData));
        Assert.That(ex!.Message, Is.EqualTo("Internal error"));
    }

    // --- SignInAsync Tests ---

    [Test]
    public async Task SignInAsync_ValidCredentials_ReturnsOkWithUser()
    {
        // Arrange
        var signData = new UserController.SignData { Email = "test@example.com", Password = "password" };
        var user = new User { Email = signData.Email, Token = "jwt_token" };

        _userServiceMock.Setup(s => s.AuthenticateAsync(signData.Email, signData.Password))
            .ReturnsAsync(user);

        // Act
        var result = await _userController.SignInAsync(signData);

        // Assert
        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);
        Assert.That(okResult!.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(okResult.Value, Is.EqualTo(user));
    }

    [Test]
    public async Task SignInAsync_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var signData = new UserController.SignData { Email = "test@example.com", Password = "wrong_password" };

        _userServiceMock.Setup(s => s.AuthenticateAsync(signData.Email, signData.Password))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _userController.SignInAsync(signData);

        // Assert
        var statusCodeResult = result.Result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public void SignInAsync_InternalError_ThrowsException()
    {
        // Arrange
        var signData = new UserController.SignData { Email = "error@example.com", Password = "password" };

        _userServiceMock.Setup(s => s.AuthenticateAsync(signData.Email, signData.Password))
            .ThrowsAsync(new Exception("Internal error"));

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(async () => await _userController.SignInAsync(signData));
        Assert.That(ex!.Message, Is.EqualTo("Internal error"));
    }

    // --- GiveAdminRightsAsync Tests ---

    [Test]
    public async Task GiveAdminRightsAsync_UserExists_ReturnsOk()
    {
        // Arrange
        const string email = "user@example.com";

        _userServiceMock.Setup(s => s.SetRoleAsync(email, "admin"))
            .ReturnsAsync(true);

        // Act
        var result = await _userController.GiveAdminRightsAsync(email);

        // Assert
        var statusCodeResult = result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }

    [Test]
    public async Task GiveAdminRightsAsync_UserDoesNotExist_ReturnsBadRequest()
    {
        // Arrange
        const string email = "unknown@example.com";

        _userServiceMock.Setup(s => s.SetRoleAsync(email, "admin"))
            .ReturnsAsync(false);

        // Act
        var result = await _userController.GiveAdminRightsAsync(email);

        // Assert
        var statusCodeResult = result as StatusCodeResult;
        Assert.That(statusCodeResult, Is.Not.Null);
        Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public void GiveAdminRightsAsync_InternalError_ThrowsException()
    {
        // Arrange
        const string email = "error@example.com";

        _userServiceMock.Setup(s => s.SetRoleAsync(email, "admin"))
            .ThrowsAsync(new Exception("Internal error"));

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(async () => await _userController.GiveAdminRightsAsync(email));
        Assert.That(ex!.Message, Is.EqualTo("Internal error"));
    }
}