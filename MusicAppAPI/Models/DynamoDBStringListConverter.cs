using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Newtonsoft.Json;

namespace MusicAppAPI.Models;

public class DynamoDBStringListConverter : IPropertyConverter
{
    public DynamoDBEntry ToEntry(object value)
    {
        if (value is not IEnumerable<string> list) return new DynamoDBNull();
        return new DynamoDBList(list.Select(x => new Primitive(x)));
    }

    public object FromEntry(DynamoDBEntry entry)
    {
        Console.WriteLine($"[Converter] Converting entry of type: {entry?.GetType().Name}");

        if (entry is DynamoDBNull || entry == null) return new List<string>();

        // Case 1: DynamoDB List (L)
        if (entry is DynamoDBList list)
        {
            return list.Entries.OfType<Primitive>().Select(p => p.AsString()).ToList();
        }
        
        // Case 2: Primitive (S or SS)
        if (entry is Primitive primitive)
        {
            // If it's a String Set (SS), AsListOfString works perfectly
            if (primitive.Type != DynamoDBEntryType.String)
            {
                return primitive.AsListOfString();
            }

            // If it's a single String (S)
            string value = primitive.AsString();
            
            // CHECK: Is it a JSON array string? e.g. "[\"id1\", \"id2\"]"
            if (value.Trim().StartsWith("[") && value.Trim().EndsWith("]"))
            {
                try 
                {
                    Console.WriteLine("[Converter] Detected JSON string array, parsing...");
                    var parsedList = JsonConvert.DeserializeObject<List<string>>(value);
                    return parsedList ?? new List<string>();
                }
                catch
                {
                    Console.WriteLine("[Converter] Failed to parse JSON, treating as single string");
                }
            }

            // Otherwise, it's just a single ID
            return new List<string> { value };
        }
        
        if (entry is PrimitiveList primitiveList)
        {
            return primitiveList.Entries.Select(p => p.AsString()).ToList();
        }

        return new List<string>();
    }
}
