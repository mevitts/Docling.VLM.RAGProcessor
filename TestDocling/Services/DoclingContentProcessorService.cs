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
using TestDocling.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static TestDocling.Services.DoclingContentProcessorService;


//gets json content from docling response and formats it into Dictionary of key: page# then PageOutput object
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
    }
    private class ImageJob
    {
        public int PageNo { get; set; }
        public string Uri { get; set; }
        public string Label { get; set; }
    }
    public async Task<Dictionary<int, PageOutput>> ProcessDoclingResponse(string doclingJsonOutput)
    {
        try
        {
            var doclingResponse = JsonSerializer.Deserialize<DoclingResponse>(doclingJsonOutput, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return await BuildPageContent(doclingResponse.Document);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON Deserialization failed for Docling response. Raw response was: {Response}", doclingJsonOutput ?? "Not available");
            throw new InvalidOperationException("Failed to process Docling response due to JSON deserialization error.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while processing Docling response. Raw response was: {Response}", doclingJsonOutput ?? "Not available");
            throw new InvalidOperationException("Failed to process Docling response due to an unexpected error.", ex);
        }
    }
    private async Task<Dictionary<int, PageOutput>> BuildPageContent(Document doclingDocument)
    {
        var fileType = Path.GetExtension(doclingDocument.Filename);
        var pageContent = new Dictionary<int, StringBuilder>();

        JsonContent json_content = doclingDocument.DoclingJsonContent;
        string mdContent = doclingDocument.MdContent;

        var allPageElements = new List<PageElementInfo>();
        //for $ref pointers, dictionary that have its key as the $ref value and its value as the object it points to
        var elementLookup = new Dictionary<string, object>();
        var imageJobs = new List<ImageJob>();


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

        //collects info from all children into allPageElements list
        void TraverseAndCollect(List<DoclingRef> childrenRefs)
        {
            if (childrenRefs == null) return;

            foreach (var childrenRef in childrenRefs)
            {
                if (elementLookup.TryGetValue(childrenRef.Ref, out object element))
                {
                    //text
                    if (element is TextItem textItem && textItem.Prov != null && textItem.Prov.Any())
                    {
                        allPageElements.Add(new PageElementInfo
                        {
                            PageNo = textItem.Prov.FirstOrDefault().PageNo,
                            Top = textItem.Prov.FirstOrDefault().Bbox?.T ?? 0,
                            Content = ContentFormatter.ConvertEscapes(textItem.Text),
                        });
                    }
                    //table
                    else if (element is TableItem tableItem && tableItem.Prov != null && tableItem.Prov.Any())
                    {
                        allPageElements.Add(new PageElementInfo
                        {
                            PageNo = tableItem.Prov.FirstOrDefault().PageNo,
                            Top = tableItem.Prov.FirstOrDefault().Bbox?.T ?? 0,
                            Content = ContentFormatter.ConvertEscapes(ConvertTable(tableItem.Data))
                        });
                    }
                    //pictures--only if file is not a PDF, as PDF images are handled separately
                    else if (element is PictureItem pictureItem && fileType != ".pdf" && pictureItem.Prov != null && pictureItem.Prov.Any())
                    {
                        if (pictureItem.Image?.Uri != null)
                        {
                            //instead of adding to allPageElements, we add to imageJobs
                            imageJobs.Add(new ImageJob
                            {
                                PageNo = (int)pictureItem.Prov.First().PageNo,
                                Uri = pictureItem.Image.Uri,
                                Label = pictureItem.Label ?? "Image"
                            });
                        }
                    }
                    else if (element is GroupItem groupItem)
                    {
                        TraverseAndCollect(groupItem.Children);
                    }
                }
            }
        }

        if (json_content.Body?.Children != null)
        {
            TraverseAndCollect(json_content.Body.Children);
        }

        //for image description in PDF files, need to extract image URIs from markdown content
        //this is because in JSON, the uri is often the full page URI
        if (fileType == ".pdf" && mdContent != null)
        {
            //regex to find image URIs in the markdown content
            var reg = new Regex(@"(?<=\!\[(.*?)\]\().*?(?=\))");
            string[] splits = mdContent.Split(new string[] { "[PAGE BREAK]" }, StringSplitOptions.None);

            int pageNo = 0;
            foreach (var page in splits)
            {
                pageNo++;
                foreach (Match match in reg.Matches(page))
                {
                    imageJobs.Add(new ImageJob
                    {
                        PageNo = pageNo,
                        Uri = match.Value,
                        Label = "Image"
                    });
                }

            }
        }

        var processedImages = new List<(int PageNo, ImageOutput Output)>();
        var imageTasks = imageJobs.Select(async job =>
        { 
            //will retry up to 3 times if VLM returns an error or invalid JSON
            const int maxTries = 3;
            for (int i = 0; i < maxTries; i++)
            {
                try
                {
                    string prompt = @"Analyze the provided image and generate a JSON object containing a 'title' and a 'description'. Both should be clear and concise, and limit the description to AT MOST 2 sentences.";

                    var imageOutput = await _vlmService.DescribeImageAsync(job.Uri, prompt, job.PageNo);

                    return (job.PageNo, imageOutput);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process image URI: {Uri} on attempt {Attempt}/{MaxTries}", job.Uri, i + 1, maxTries);

                    if (i < maxTries - 1)
                    {
                        //wait to prevent spamming API
                        await Task.Delay(2000);
                    }
                }
            }
            _logger.LogError("All {MaxTries} retries failed for image URI: {Uri}", maxTries, job.Uri);
            return (job.PageNo, new ImageOutput { Title = job.Label, Description = "Error: Failed to process image after multiple attempts." });
        }).ToList();

        var results = await Task.WhenAll(imageTasks);
        processedImages.AddRange(results);

        var finalOutput = new Dictionary<int, PageOutput>();
        foreach (var textElement in allPageElements)
        {
            if (textElement.PageNo.HasValue)
            {
                int pageKey = textElement.PageNo.Value;
                if (!finalOutput.TryGetValue(pageKey, out var pageOutput))
                {
                    pageOutput = new PageOutput();
                    finalOutput[pageKey] = pageOutput;
                }
                //keep a running StringBuilder per page for efficiency
                if (pageOutput.Text == null)
                    pageOutput.Text = "";
                if (!string.IsNullOrEmpty(pageOutput.Text))
                    pageOutput.Text += "\n";
                pageOutput.Text += textElement.Content;
            }
        }
        foreach (var processedImage in processedImages)
        {
            int pageKey = processedImage.PageNo;
            if (!finalOutput.TryGetValue(pageKey, out var pageOutput))
            {
                pageOutput = new PageOutput();
                finalOutput[pageKey] = pageOutput;
            }
            pageOutput.Images.Add(processedImage.Output);
        }
        return finalOutput.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

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
