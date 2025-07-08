using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using TestDocling;
using System.Text;
using TestDocling.Models;

namespace TestDocling;

public class JsonProg
{
    public static Dictionary<string, string> GetJsonContent(string json)
    {
        Dictionary<string, string> fileContents = new Dictionary<string, string>();
        try
        {
            TaskResultResponse doclingResponse = JsonSerializer.Deserialize<TaskResultResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

            if (doclingResponse?.Document != null)
            {
                fileContents.Add("filename", doclingResponse.Document.Filename);
                fileContents.Add("md_content", doclingResponse.Document.MdContent);
            }
            else
            {
                Console.WriteLine("Did not deserialize.");
            }
        }
        catch (JsonException ex) {
            Console.WriteLine($"JSON Deserialization Error: {ex.Message}");
            throw; // Optionally rethrow for higher-level handling
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            throw;
        }
        return fileContents;
    }
}