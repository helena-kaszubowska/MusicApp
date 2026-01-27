using EasyNetQ;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MusicAppAPI.Models;

namespace MusicAppAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlbumsController : ControllerBase
{
    private readonly IMongoCollection<Album> _albumsCollection;
    private readonly IMongoCollection<Track> _tracksCollection;
    private readonly IBus _rabbitMQBus;

    public AlbumsController(IMongoCollection<Album> albumsCollection, IMongoCollection<Track> tracksCollection, IBus rabbitMQBus)
    {
        _albumsCollection = albumsCollection;
        _tracksCollection = tracksCollection;
        _rabbitMQBus = rabbitMQBus;
    }

    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<ActionResult> AddNewAlbum([FromBody] Album album)
    {
        if (album.Title is null) return StatusCode(StatusCodes.Status400BadRequest, "Title must be specified.");
        if (album.Artist is null) return StatusCode(StatusCodes.Status400BadRequest, "Artist must be specified.");
        if (album.Year is null) return StatusCode(StatusCodes.Status400BadRequest, "Year must be specified.");
        if (album.Tracks == null || album.Tracks.Count == 0)
            return StatusCode(StatusCodes.Status400BadRequest, "Album must have at least one track.");

        // Validate tracks
        if (album.Tracks.Any(t => t.Title == null || t.Length == null))
            return StatusCode(StatusCodes.Status400BadRequest, "Some tracks are missing title and/or length.");
        if (album.Tracks.Any(t => t.Nr == null))
            for (int i = 0; i < album.Tracks.Count; i++)
                album.Tracks[i].Nr = i + 1;

        // Generate an id if it's not provided
        album.Id ??= ObjectId.GenerateNewId().ToString();

        foreach (Track track in album.Tracks)
        {
            track.Artist ??= album.Artist;
            track.AlbumTitle = album.Title;
            track.AlbumId = album.Id;
            track.Year ??= album.Year;
        }

        // Store tracks in a separate collection
        await _tracksCollection.InsertManyAsync(album.Tracks);

        // Only track ids are stored with the album in the database
        album.TrackIds = album.Tracks.Select(t => t.Id!).ToList();
        album.Tracks = null;
        
        await _albumsCollection.InsertOneAsync(album);
        return Created($"api/albums/{album.Id}", null);
    }

    [HttpGet]
    public async Task<ActionResult<List<Album>>> GetAllAlbums()
    {
        FilterDefinition<Album>? filter = Builders<Album>.Filter.Empty;

        // When requesting all the albums, tracks are usually not needed and create too much boilerplate
        ProjectionDefinition<Album>? projection = Builders<Album>.Projection.Exclude("tracks");
        return Ok(await _albumsCollection.Find(filter).Project<Album>(projection).ToListAsync());
    }

    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateAlbum(string id, [FromBody] Album albumUpdate)
    {
        if (albumUpdate.Title is null) return StatusCode(StatusCodes.Status400BadRequest, "Title must be specified.");
        if (albumUpdate.Artist is null) return StatusCode(StatusCodes.Status400BadRequest, "Artist must be specified.");
        if (albumUpdate.Year is null) return StatusCode(StatusCodes.Status400BadRequest, "Year must be specified.");
        if (albumUpdate.Tracks == null || albumUpdate.Tracks.Count == 0)
            return StatusCode(StatusCodes.Status400BadRequest, "Album must have at least one track.");
        
        // Fetch existing album to get trackIds
        FilterDefinition<Album> filter = Builders<Album>.Filter.Eq(a => a.Id, id);
        Album? album = await _albumsCollection.Find(filter).FirstOrDefaultAsync();
        if (album == null) return StatusCode(StatusCodes.Status404NotFound);

        // Validate tracks
        if (albumUpdate.Tracks!.Any(t => t.Title == null || t.Length == null))
            return StatusCode(StatusCodes.Status400BadRequest, "Some tracks are missing title and/or length.");
        if (albumUpdate.Tracks!.Any(t => t.Nr == null))
            for (int i = 0; i < albumUpdate.Tracks!.Count; i++)
                albumUpdate.Tracks[i].Nr = i + 1;
        foreach (Track track in albumUpdate.Tracks!)
        {
            track.Artist ??= album.Artist;
            track.AlbumTitle = album.Title;
            track.AlbumId = id;
            track.Year ??= album.Year;
        }

        // Compare tracks to identify removals
        List<string> existingTrackIds = album.TrackIds!;
        List<string> updatedTrackIds = albumUpdate.Tracks.Where(t => t.Id != null).Select(t => t.Id!).ToList();
        List<string> tracksToRemove = existingTrackIds.Except(updatedTrackIds).ToList();

        // Use bulk write for tracks for better performance
        List<WriteModel<Track>> bulkTrackOperations = [];

        // New tracks (id is null)
        List<Track> newTracks = albumUpdate.Tracks.Where(t => t.Id is null).ToList();
        foreach (Track newTrack in newTracks)
            bulkTrackOperations.Add(new InsertOneModel<Track>(newTrack));

        // Existing tracks (id is non-null)
        List<Track> existingTracks = albumUpdate.Tracks.Where(t => t.Id != null).ToList();
        foreach (Track track in existingTracks)
            bulkTrackOperations.Add(new ReplaceOneModel<Track>(
                Builders<Track>.Filter.Eq(t => t.Id, track.Id), track));

        // Tracks to remove
        if (tracksToRemove.Count != 0)
            bulkTrackOperations.Add(new DeleteManyModel<Track>(
                Builders<Track>.Filter.In(t => t.Id, tracksToRemove)));
        foreach (string trackId in tracksToRemove)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "assets/audio", trackId + ".flac");
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        if (bulkTrackOperations.Count != 0)
            await _tracksCollection.BulkWriteAsync(bulkTrackOperations);
        
        // Only track ids are stored with the album in the database
        albumUpdate.TrackIds = albumUpdate.Tracks.Select(t => t.Id!).ToList();
        albumUpdate.Tracks = null;
        
        await _albumsCollection.ReplaceOneAsync(filter, albumUpdate);

        return StatusCode(StatusCodes.Status200OK);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAlbum(string id)
    {
        FilterDefinition<Album>? filter = Builders<Album>.Filter.Eq(a => a.Id, id);

        Album? album = await _albumsCollection.Find(filter).FirstOrDefaultAsync();
        DeleteResult result = await _albumsCollection.DeleteOneAsync(filter);
        if (result.DeletedCount == 0) return StatusCode(StatusCodes.Status404NotFound);

        // Remove all tracks of the album as well
        await _tracksCollection.DeleteManyAsync(Builders<Track>.Filter.Eq(t => t.AlbumId, id));
        
        foreach (string trackId in album!.TrackIds!)
            System.IO.File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "assets/audio", trackId + ".flac"));

        return StatusCode(StatusCodes.Status200OK);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Album>> GetAlbumById(string id)
    {
        FilterDefinition<Album>? filter = Builders<Album>.Filter.Eq(t => t.Id, id);

        // When a single album is requested, it's better to return tracks details as well, not just ids
        var album = BsonSerializer.Deserialize<Album?>(await _albumsCollection.Aggregate().Match(filter)
            .Lookup("tracks", "tracks", "_id", "realTracks").FirstOrDefaultAsync());

        if (album is null) return StatusCode(StatusCodes.Status404NotFound);

        // These fields are useless in the context of an album
        foreach (Track track in album.Tracks!)
        {
            track.AlbumTitle = null;
            track.AlbumId = null;
        }

        // Send a message to RabbitMQ to track album views
        // (do not await for it to finish in case RabbitMQ is not available)
        Task.Run(async () =>
        {
            try
            {
                AlbumViewedMessage message = new()
                {
                    AlbumId = album.Id!,
                    AlbumTitle = album.Title!,
                    AlbumArtist = album.Artist!,
                    ViewedAt = DateTime.UtcNow,
                    Source =
                        $"{Request.HttpContext.Connection.RemoteIpAddress}:{Request.HttpContext.Connection.RemotePort}"
                };

                await _rabbitMQBus.PubSub.PublishAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        });
        
        // Return the response even if RabbitMQ is down
        return Ok(album);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<Album>>> SearchAlbums(string query)
    {
        // Search by titles and artists
        string escapedQuery = System.Text.RegularExpressions.Regex.Escape(query);
        FilterDefinition<Album>? filter = Builders<Album>.Filter.Or(
            Builders<Album>.Filter.Regex("title", new BsonRegularExpression($"^{escapedQuery}", "i")),
            Builders<Album>.Filter.Regex("artist", new BsonRegularExpression($"^{escapedQuery}", "i")));

        // Multiple albums might be returned, and tracks could create too much boilerplate
        ProjectionDefinition<Album>? projection = Builders<Album>.Projection.Exclude("tracks");
        return Ok(await _albumsCollection.Find(filter).Project<Album>(projection).ToListAsync());
    }
}
