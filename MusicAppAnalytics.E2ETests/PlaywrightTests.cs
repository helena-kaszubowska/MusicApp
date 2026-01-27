using System.Net;
using System.Text.Json;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace MusicAppAnalytics.E2ETests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class PlaywrightTests : PlaywrightTest
{
    private IAPIRequestContext _requestContext;
    private const string BaseUrl = "http://localhost:5048"; 

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

    [Test]
    public async Task GetTopAlbums_ReturnsOkAndList()
    {
        // Act
        var response = await _requestContext.GetAsync("/api/analytics/top-albums");

        // Assert
        Assert.That(response.Status, Is.EqualTo((int)HttpStatusCode.OK));
        
        var json = await response.JsonAsync();
        Assert.That(json?.ValueKind, Is.EqualTo(JsonValueKind.Array));
    }

    [Test]
    public async Task GetTopTracks_WithArtistFilterAndSpecifiedCount_ReturnsOkAndList()
    {
        // Act
        var response = await _requestContext.GetAsync("/api/analytics/top-tracks?artist=architects&count=5");

        // Assert
        Assert.That(response.Status, Is.EqualTo((int)HttpStatusCode.OK));
        
        // 3. Assert that results match the filter
        var tracks = (await response.JsonAsync())?.EnumerateArray() ?? Enumerable.Empty<JsonElement>();
        var correctMatches = tracks.Count() < 5 ? true : tracks
            .All(t => t.GetProperty("artist").GetString()?.Contains("architects", StringComparison.CurrentCultureIgnoreCase) ?? false);
        Assert.That(correctMatches, Is.True);
    }
}