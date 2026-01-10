using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.DataModel;

namespace MusicAppAPI.Models;

[DynamoDBTable("Tracks")]
public class Track
{
    [DynamoDBHashKey("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }
    
    [DynamoDBProperty("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }
    
    [DynamoDBProperty("artist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Artist { get; set; }
    
    [DynamoDBProperty("year")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Year { get; set; }
    
    [DynamoDBProperty("length")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Length { get; set; }
    
    [DynamoDBProperty("genre")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Genre { get; set; }
    
    [DynamoDBProperty("nr")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Nr { get; set; }
    
    [DynamoDBProperty("albumTitle")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AlbumTitle { get; set; }
    
    [DynamoDBProperty("albumId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AlbumId { get; set; }
}
