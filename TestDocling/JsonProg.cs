using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using TestDocling;
using System.Text;

namespace TestDocling
{
    public class JsonProg
    {
        public static Dictionary<string, string> GetJsonContent(string json)
        {
            Dictionary<string, string> fileContents = new Dictionary<string, string>();
            try
            {
                DoclingResponse doclingResponse = JsonSerializer.Deserialize<DoclingResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

                if (doclingResponse?.Document != null)
                {
                    fileContents.Add("filename", doclingResponse.Document.Filename);

                    fileContents.Add("md_content", doclingResponse.Document.MdContent);

                    // serialize Json Content so can extract page content
                    /*string json_content = JsonSerializer.Serialize(doclingResponse.Document.DoclingJsonContent, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true
                    });*/

                    var built_content = PageContentBuilder(doclingResponse.Document);


                    fileContents.Add("json_content", json_content);
                }
                else
                {
                    Console.WriteLine("Did not deserialize.");
                }

                // Add page count to the dictionary if available
                if (doclingResponse.Document.DoclingJsonContent?.Pages != null)
                {
                    fileContents.Add("total_pages", doclingResponse.Document.DoclingJsonContent.Pages.Count.ToString());
                }
                else
                {
                    Console.WriteLine("Pages data not found in JSON.");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON Deserialization Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            }

            return fileContents;
        }
    }
}
