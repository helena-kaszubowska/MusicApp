using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.DataModel;

namespace MusicAppAPI.Models;

[DynamoDBTable("users")]
public class User
{
    [DynamoDBHashKey("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }
    
    [DynamoDBProperty("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; set; }
    
    [DynamoDBProperty("password")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }
    
    [DynamoDBProperty("roles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Roles { get; set; }
    
    [DynamoDBIgnore]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Token { get; set; }
    
    [DynamoDBProperty("libraryTracks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? LibraryTracks { get; set; }
    
    [DynamoDBProperty("libraryAlbums")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? LibraryAlbums { get; set; }
}
