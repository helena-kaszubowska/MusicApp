using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Newtonsoft.Json;

namespace MusicAppAPI.Models;

public class DynamoDBStringListConverter : IPropertyConverter
{
    public DynamoDBEntry ToEntry(object value)
    {
        if (value is not IEnumerable<string> list) return new DynamoDBNull();
        
        // OPTION A: Write as Native List (Recommended for long term)
        // return new DynamoDBList(list.Select(x => new Primitive(x)));

        // OPTION B: Write as JSON String (Matches your current DMS migration output)
        return new Primitive(JsonConvert.SerializeObject(list));
    }

    public object FromEntry(DynamoDBEntry entry)
    {
        // Console.WriteLine($"[Converter] Converting entry of type: {entry?.GetType().Name}");

        if (entry is DynamoDBNull || entry == null) return new List<string>();

        // Case 1: DynamoDB List (L) - If you ever switch to native lists
        if (entry is DynamoDBList list)
        {
            return list.Entries.OfType<Primitive>().Select(p => p.AsString()).ToList();
        }
        
        // Case 2: Primitive (S or SS)
        if (entry is Primitive primitive)
        {
            if (primitive.Type != DynamoDBEntryType.String)
            {
                return primitive.AsListOfString();
            }

            string value = primitive.AsString();
            
            // Check for JSON array
            if (value.Trim().StartsWith("[") && value.Trim().EndsWith("]"))
            {
                try 
                {
                    var parsedList = JsonConvert.DeserializeObject<List<string>>(value);
                    return parsedList ?? new List<string>();
                }
                catch
                {
                    // Ignore parse error
                }
            }

            return new List<string> { value };
        }
        
        if (entry is PrimitiveList primitiveList)
        {
            return primitiveList.Entries.Select(p => p.AsString()).ToList();
        }

        return new List<string>();
    }
}
