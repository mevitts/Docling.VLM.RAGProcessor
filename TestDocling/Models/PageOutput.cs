using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace TestDocling.Models;

public class PageOutput
{
    public string Text { get; set; } = string.Empty;
    public List<ImageOutput> Images { get; set; } = new List<ImageOutput>();
}
public class ImageOutput
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }
}
