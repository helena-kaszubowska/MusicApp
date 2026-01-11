using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.DataModel;

namespace MusicAppAPI.Models;

[DynamoDBTable("albums")]
public class Album
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
    
    [DynamoDBIgnore]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Track>? Tracks { get; set; }
    
    [DynamoDBProperty("coverUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CoverUrl { get; set; }
    
    [DynamoDBProperty("label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; set; }
    
    // This property is only used to serialize and deserialize album objects from DynamoDB
    [DynamoDBProperty("trackIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public List<string>? TrackIds { get; set; }
}
