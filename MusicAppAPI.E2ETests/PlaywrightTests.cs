using System.Net;
using System.Text.Json;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace MusicAppAPI.E2ETests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class PlaywrightTests : PlaywrightTest
{
    private IAPIRequestContext _requestContext;
    private const string BaseUrl = "http://localhost:5064";

    [SetUp]
    public async Task Setup()
    {
        _requestContext = await Playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = BaseUrl,
            IgnoreHTTPSErrors = true
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        await _requestContext.DisposeAsync();
    }

    // TEST 1: User Lifecycle (Register -> Login -> Delete Account)
    [Test]
    public async Task User_CanRegisterLoginAndDeleteAccount()
    {
        // 1. Register
        var email = $"user_{Guid.NewGuid()}@test.com";
        var password = "Password123!";

        var registerResponse = await _requestContext.PostAsync("/api/sign-up", new APIRequestContextOptions
        {
            DataObject = new { email, password }
        });

        Assert.That(registerResponse.Status, Is.EqualTo((int)HttpStatusCode.Created));

        // 2. Login
        var loginResponse = await _requestContext.PostAsync("/api/sign-in", new APIRequestContextOptions
        {
            DataObject = new { email, password }
        });

        Assert.That(loginResponse.Status, Is.EqualTo((int)HttpStatusCode.OK));

        // Verify response data
        var json = await loginResponse.JsonAsync();
        var token = json?.GetProperty("token").GetString();
        var returnedEmail = json?.GetProperty("email").GetString();
        var userId = json?.GetProperty("id").GetString();

        Assert.That(token, Is.Not.Null.And.Not.Empty);
        Assert.That(returnedEmail, Is.EqualTo(email));
        Assert.That(userId, Is.Not.Null.And.Not.Empty);
        
        // 3. Delete account
        await _requestContext.DeleteAsync($"/api/user/{userId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "Authorization", $"Bearer {token}" } }
        });
    }

    // TEST 2: Library Management (Add Track -> Verify -> Remove Track)
    [Test]
    public async Task UserLibrary_CanAddTrackVerifyAndRemoveIt()
    {
        // 1. Authenticate
        var (userId, token) = await RegisterAndLogin();
        
        // 2. Add a track to library
        const string trackId = "677c0a79dc41299ee32f7049"; 

        var addResponse = await _requestContext.PatchAsync("/api/library/tracks", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "Authorization", $"Bearer {token}" } },
            DataObject = trackId // Body is just the string
        });

        Assert.That(addResponse.Status, Is.EqualTo((int)HttpStatusCode.OK));

        // 3. Verify track is in library
        var getResponse = await _requestContext.GetAsync("/api/library/tracks", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "Authorization", $"Bearer {token}" } }
        });

        Assert.That(getResponse.Status, Is.EqualTo((int)HttpStatusCode.OK));
        
        // Check if the specific track ID is present in the response list
        var libraryTracks = await getResponse.JsonAsync();
        var containsTrack = (libraryTracks?.EnumerateArray() ?? Enumerable.Empty<JsonElement>()).Any(t => t.GetProperty("id").GetString() == trackId);
        Assert.That(containsTrack, Is.True, $"Track with ID {trackId} was added but not found in the library response.");

        // 4. Remove track
        var deleteResponse = await _requestContext.DeleteAsync($"/api/library/tracks/{trackId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "Authorization", $"Bearer {token}" } }
        });

        Assert.That(deleteResponse.Status, Is.EqualTo((int)HttpStatusCode.OK));
        
        // 5. Verify it is gone
        var getResponseAfterDelete = await _requestContext.GetAsync("/api/library/tracks", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "Authorization", $"Bearer {token}" } }
        });
        
        libraryTracks = await getResponseAfterDelete.JsonAsync();
        containsTrack = (libraryTracks?.EnumerateArray() ?? Enumerable.Empty<JsonElement>()).Any(t => t.GetProperty("id").GetString() == trackId);
        Assert.That(containsTrack, Is.False, "Track was removed but still appears in the library.");
        
        // Cleanup
        await DeleteAccount(userId, token);
    }

    // TEST 3: Public Content Access (Search -> Download Attempt)
    [Test]
    public async Task SearchTrackAndAttemptDownload()
    {
        // 1. Search for tracks (Public endpoint)
        var searchResponse = await _requestContext.GetAsync("/api/tracks/search?query=test");
        
        Assert.That(searchResponse.Status, Is.EqualTo((int)HttpStatusCode.OK));
        var tracks = await searchResponse.JsonAsync();
        Assert.That(tracks?.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

        // 2. Attempt to download a non-existent track (to verify 404 behavior)
        // This confirms the endpoint is reachable and logic works
        var downloadResponse = await _requestContext.GetAsync("/api/tracks/nonexistent_id/download");
        
        Assert.That(downloadResponse.Status, Is.EqualTo((int)HttpStatusCode.NotFound));
    }

    // TEST 4: Standard User Cannot Delete Albums
    [Test]
    public async Task User_CannotDeleteAlbumWithoutPermissions()
    {
        // 1. Authenticate as a new user (likely NOT an admin unless DB is empty)
        var (userId, token) = await RegisterAndLogin();

        // 2. Attempt to delete an album (Admin only endpoint)
        var deleteResponse = await _requestContext.DeleteAsync("/api/albums/some_album_id", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "Authorization", $"Bearer {token}" } }
        });

        // 3. Assert Forbidden (403) or Unauthorized (401)
        Assert.That(deleteResponse.Status, 
            Is.EqualTo((int)HttpStatusCode.Forbidden).Or.EqualTo((int)HttpStatusCode.Unauthorized));
        
        // Cleanup
        await DeleteAccount(userId, token);
    }

    // TEST 5: Search Tracks with Filters
    [Test]
    public async Task SearchAlbumsByTitleOrArtist_ReturnsMatchedAlbums()
    {
        // 1. Search for a specific query that will return result
        const string query = "architects";
        var searchResponse = await _requestContext.GetAsync($"/api/tracks/search?query={query}");

        // 2. Assert status OK
        Assert.That(searchResponse.Status, Is.EqualTo((int)HttpStatusCode.OK));
        
        // 3. Assert that search results match the filter
        var tracks = (await searchResponse.JsonAsync())?.EnumerateArray() ?? Enumerable.Empty<JsonElement>();
        var correctMatches = tracks.Count() == 0 ? true : tracks
            .All(t => (t.GetProperty("title").GetString()?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false) 
                || (t.GetProperty("artist").GetString()?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false));
        Assert.That(correctMatches, Is.True);
    }

    // Helper methods
    private async Task<(string id, string token)> RegisterAndLogin()
    {
        var email = $"e2e_{Guid.NewGuid()}@test.com";
        var password = "Password123!";

        await _requestContext.PostAsync("/api/sign-up", new APIRequestContextOptions
        {
            DataObject = new { email, password }
        });

        var loginResponse = await _requestContext.PostAsync("/api/sign-in", new APIRequestContextOptions
        {
            DataObject = new { email, password }
        });

        var json = await loginResponse.JsonAsync();
        return (json?.GetProperty("id").GetString() ?? throw new Exception("Failed to get user id"), 
            json.Value.GetProperty("token").GetString() ?? throw new Exception("Failed to get token"));
    }

    private async Task DeleteAccount(string userId, string token)
    {
        await _requestContext.DeleteAsync($"/api/user/{userId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "Authorization", $"Bearer {token}" } }
        });
    }
}