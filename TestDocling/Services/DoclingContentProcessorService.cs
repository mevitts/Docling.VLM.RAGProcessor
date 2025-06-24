namespace TestDocling.Services;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using OllamaSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TestDocling;
using TestDocling.Helpers;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static TestDocling.Services.DoclingContentProcessorService;

//gets json content from docling response and formats it into Dictionary of key: page# then value: text information
public class DoclingContentProcessorService : IDoclingContentProcessorService
{
    private readonly ILogger<DoclingContentProcessorService> _logger;
    private readonly IVlmService _vlmService;

    public DoclingContentProcessorService(IHttpClientFactory httpClientFactory, ILogger<DoclingContentProcessorService> logger, IVlmService vlmService)
    {
        _logger = logger;
        _vlmService = vlmService;
    }
    private class PageElementInfo
    {
        public int? PageNo { get; set; }
        public double Top { get; set; }
        public string Content { get; set; }
        public string ElementType { get; set; } //e.g. text, group, table, picture
        public string ElementId { get; set; }
    }
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
    private class ImageJob
    {
        public int PageNo { get; set; }
        public string Uri { get; set; }
        public string Label { get; set; }
    }//helper to process images HELPER

    public async Task<Dictionary<int, PageOutput>> ProcessDoclingResponse(string doclingJsonOutput)
    {
        // Deserialize JSON string into DoclingResponse object
        var doclingResult = JsonSerializer.Deserialize<DoclingResponse>(doclingJsonOutput, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        //use method to build page-content
        return await BuildPageContent(doclingResult.Document);

    }//end of ProcessDoclingResponse
    private async Task<Dictionary<int, PageOutput>> BuildPageContent(Document doclingDocument)
    {
        var fileType = Path.GetExtension(doclingDocument.Filename);
        var pageContent = new Dictionary<int, StringBuilder>();
        JsonContent json_content = doclingDocument.DoclingJsonContent;
        string md_content = doclingDocument.MdContent;

        //page info has #, top of element, content, and type (text, table, etc.)
        var allPageElements = new List<PageElementInfo>();
        //for $ref pointers, dictionary that have its key as the $ref value and its value as the object it points to
        var elementLookup = new Dictionary<string, object>();
        //for images, a list of image jobs to process later
        var imageJobs = new List<ImageJob>();

        // --- Simple and Clear Element Lookup Population ---
        if (json_content.Texts != null)
        {
            foreach (var item in json_content.Texts)
            {
                elementLookup[item.SelfRef] = item;
            }
        }
        if (json_content.Groups != null)
        {
            foreach (var item in json_content.Groups)
            {
                elementLookup[item.SelfRef] = item;
            }
        }
        if (json_content.Tables != null)
        {
            foreach (var item in json_content.Tables)
            {
                elementLookup[item.SelfRef] = item;
            }
        }
        if (json_content.Pictures != null)
        {
            foreach (var item in json_content.Pictures)
            {
                elementLookup[item.SelfRef] = item;
            }
        }

        //collects info from all children 
        void TraverseAndCollect(List<DoclingRef> childrenRefs)
        {
            if (childrenRefs == null) return;

            foreach (var childrenRef in childrenRefs)
            {
                if (elementLookup.TryGetValue(childrenRef.Ref, out object element))
                {
                    //depending on element, fills out page info
                    if (element is TextItem textItem && textItem.Prov != null && textItem.Prov.Any())
                    {
                        allPageElements.Add(new PageElementInfo
                        {
                            PageNo = textItem.Prov.FirstOrDefault().PageNo,
                            Top = textItem.Prov.FirstOrDefault().Bbox?.T ?? 0,
                            Content = ContentFormatter.ConvertEscapes(textItem.Text),
                        });

                    }//for textitem

                    else if (element is TableItem tableItem && tableItem.Prov != null && tableItem.Prov.Any())
                    {
                        allPageElements.Add(new PageElementInfo
                        {
                            PageNo = tableItem.Prov.FirstOrDefault().PageNo,
                            Top = tableItem.Prov.FirstOrDefault().Bbox?.T ?? 0,
                            Content = ContentFormatter.ConvertEscapes(ConvertTable(tableItem.Data))
                        });
                    }//for tableitem

                    else if (element is PictureItem pictureItem && fileType != ".pdf" && pictureItem.Prov != null && pictureItem.Prov.Any())
                    {
                        if (pictureItem.Image?.Uri != null)
                        {
                            imageJobs.Add(new ImageJob
                            {
                                PageNo = (int)pictureItem.Prov.First().PageNo,
                                Uri = pictureItem.Image.Uri,
                                Label = pictureItem.Label ?? "Image"
                            });
                        }//add image job to list for later processing
                    }//for pictureitem

                    else if (element is GroupItem groupItem)
                    {
                        TraverseAndCollect(groupItem.Children);
                    }//traverse if Groupitem

                }//gets child element

            } //traverse through all children

        }//end of TraverseAndCollect

        if (json_content.Body?.Children != null)
        {
            TraverseAndCollect(json_content.Body.Children);
        }//call TraverseAndCollect to start

        if (fileType == ".pdf" && md_content != null)
        {
            //regex to find image URIs in the markdown content
            //now splits into groups to capture the URI correctly
            var reg = new Regex(@"(?<=\!\[(.*?)\]\().*?(?=\))");
            //splits md content by page
            string[] splits = md_content.Split(new string[] { "[PAGE BREAK]" }, StringSplitOptions.None);

            int pageNo = 0;
            foreach (var page in splits)
            {
                pageNo++;
                foreach (Match match in reg.Matches(page))
                {
                    imageJobs.Add(new ImageJob
                    {
                        PageNo = pageNo,
                        Uri = match.Value, //uri placed in group 1
                        Label = "Image"
                    });
                }

            }//goes through each page element (string), finds regex matches to get image uris, then adds as image job

        }//collect image information through markdown content, as json returns uris of the full page sometimes, leading to inaccurate image description

        //process all images in parallel
        var processedImages = new List<(int PageNo, ImageOutput Output)>();
        var imageTasks = imageJobs.Select(async job =>
        {
            const int maxTries = 3;
            for (int i = 0; i < maxTries; i++)
            {
                _logger.LogInformation("Try {count}", i);
                string descriptionJson = "";
                try
                {
                    string prompt = @"You are an automated image analysis tool. Your SOLE function is to return a single, valid JSON object.
Do NOT include any explanatory text, apologies, or any characters before or after the JSON object.
Do NOT use markdown code blocks like ```json.

Respond ONLY with JSON that adheres to this exact structure:
{
  ""title"": ""A descriptive title of the image"",
  ""description"": ""A detailed description of the image content.""
}";
                    descriptionJson = await _vlmService.DescribeImageAsync(job.Uri, prompt);
                    _logger.LogInformation("VLM response: {Response}", descriptionJson);

                    //clean up VLM response
                    var match = Regex.Match(descriptionJson, @"\{.*\}", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        string cleanedJson = match.Value;
                        var imageOut = JsonSerializer.Deserialize<ImageOutput>(cleanedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        return (job.PageNo, imageOut);
                    }//if found a match with the regex, that is text between initial and last {}, eliminating unnecessary VLM content
                    else
                    {
                        _logger.LogWarning("Could not find a valid JSON object for URI: {Uri} on attempt {Attempt}. Response: {Response}", job.Uri, i + 1, descriptionJson);
                    }//if no JSON outputted, wiill log this error then retry

                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "JSON Deserialization failed for URI: {Uri}. Raw VLM response was: {Response}", job.Uri, descriptionJson ?? "Not available");
                    
                }//deserialization errors

                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unexpected error occurred while processing image URI: {Uri}. Raw VLM response was: {Response}", job.Uri, descriptionJson ?? "Not available");
                    //return a failed state so the process doesn't cras
                    break;
                }
            }//define number of retries in case VLM has erroneous outputs
            _logger.LogError("All {MaxRetries} retries failed for image URI: {Uri}", maxTries, job.Uri);
            return (job.PageNo, new ImageOutput { Title = job.Label, Description = "Error: Failed to process image after multiple attempts." });

        }).ToList();

        var results = await Task.WhenAll(imageTasks);
        processedImages.AddRange(results);

        _logger.LogInformation("Processed {Count} images from document.", processedImages.Count);
        // final output dictionary to hold page number and new PageOutput object which also has ImageOutput
        var finalOutput = new Dictionary<int, PageOutput>();

        //group texts by page number
        var textElementsByPage = new Dictionary<int, List<PageElementInfo>>();
        foreach (var textElement in allPageElements)
        {
            if (textElement.PageNo.HasValue)
            {
                int pageKey = textElement.PageNo.Value;
                if (!textElementsByPage.ContainsKey(pageKey))
                {
                    textElementsByPage[pageKey] = new List<PageElementInfo>();
                }
                textElementsByPage[pageKey].Add(textElement);
            }
        }

        //group images by page number
        var imagesByPage = new Dictionary<int, List<ImageOutput>>();
        foreach (var processedImage in processedImages)
        {
            int pageKey = processedImage.PageNo;
            if (!imagesByPage.ContainsKey(pageKey))
            {
                imagesByPage[pageKey] = new List<ImageOutput>();
            }
            imagesByPage[pageKey].Add(processedImage.Output);
        }


        // Get a unique, sorted list of all page numbers from both text and images
        var allPageNumbersSet = new HashSet<int>();
        foreach (var pageKey in textElementsByPage.Keys)
        {
            allPageNumbersSet.Add(pageKey);
        }
        foreach (var pageKey in imagesByPage.Keys)
        {
            allPageNumbersSet.Add(pageKey);
        }

        //initialize list to hold sorted page numbers
        var sortedPageNumbers = new List<int>(allPageNumbersSet);
        sortedPageNumbers.Sort();

        foreach (var pageNumber in sortedPageNumbers)
        {
            var pageOutput = new PageOutput();

            if (textElementsByPage.ContainsKey(pageNumber))
            {
                var textElements = textElementsByPage[pageNumber];

                //sort elements by their top on page
                textElements.Sort((e1, e2) => e1.Top.CompareTo(e2.Top));

                var textBuilder = new System.Text.StringBuilder();
                for (int i = 0; i < textElements.Count; i++)
                {
                    textBuilder.Append(textElements[i].Content);
                    if (i < textElements.Count - 1)
                    {
                        textBuilder.Append("\n");
                    }//adds newline after each element except the last one
                }//builds text content for the page
                pageOutput.Text = textBuilder.ToString();
            }//for each pageNumber, if there are text elements, build the text content

            // Add image content for the current page
            if (imagesByPage.ContainsKey(pageNumber))
            {
                pageOutput.Images.AddRange(imagesByPage[pageNumber]);
            }

            finalOutput[pageNumber] = pageOutput;

        }//end of foreach pageNumber

        _logger.LogInformation("Finished processing document content.");
        return finalOutput;
    }

    //helper to convert table data to string format
    private string ConvertTable(TableData tableData)
    {
        if (tableData?.TableCells == null || tableData.TableCells.Count == 0)
        {
            return "Empty Table";
        }
        var tableBuilder = new StringBuilder();
        tableBuilder.AppendLine("--- TABLE START ---");
        foreach (var cell in tableData.TableCells)
        {
            tableBuilder.AppendLine($"- Cell [{cell.StartRowOffsetIDX},{cell.StartColOffsetIDX}]: {cell.Text}");
        }
        tableBuilder.AppendLine("--- TABLE END ---");
        return tableBuilder.ToString();
    }

}
