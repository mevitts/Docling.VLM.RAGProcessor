﻿namespace TestDocling.Services;

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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TestDocling;
using TestDocling.Helpers;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
    public async Task<Dictionary<int, string>> ProcessDoclingResponse(string doclingJsonOutput)
    {
        // Deserialize JSON string into DoclingResponse object
        var doclingResult = JsonSerializer.Deserialize<DoclingResponse>(doclingJsonOutput, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        //use method to build page-content
        return await BuildPageContent(doclingResult.Document);

    }//end of ProcessDoclingResponse
    private async Task<Dictionary<int, string>> BuildPageContent(Document doclingDocument)
    {
        var fileType = Path.GetExtension(doclingDocument.Filename);
        var pageContent = new Dictionary<int, StringBuilder>();
        //for $ref pointers, dictionary that have its key as the $ref value and its value as the object it points to
        var elementLookup = new Dictionary<string, object>();

        JsonContent json_content = doclingDocument.DoclingJsonContent;
        string md_content = doclingDocument.MdContent;

        //fills out elementLookup with all elements in the returned json content with their selfref
        if (json_content.Texts != null)
        {
            foreach (TextItem textItem in json_content.Texts)
            {
                elementLookup[textItem.SelfRef] = textItem;
            }
        }
        if (json_content.Groups != null)
        {
            foreach (var groupItem in json_content.Groups)
            {
                elementLookup[groupItem.SelfRef] = groupItem;
            }
        }
        if (json_content.Tables != null)
        {
            foreach (var tableItem in json_content.Tables)
            {
                elementLookup[tableItem.SelfRef] = tableItem;
            }
        }
        if (json_content.Pictures != null)
        {
            foreach (var pictureItem in json_content.Pictures)
            {
                elementLookup[pictureItem.SelfRef] = pictureItem;
            }
        }




        //collect all content in order with page and position
        //page info has #, top of element, content, and type (text, table, etc.)
        var allPageElements = new List<PageElementInfo>();


        //recursive function to traverse all elements and convert them to a page element then add to temporary page dictionary
        async Task TraverseAndCollect(List<DoclingRef> childrenRefs)
        {
            if (childrenRefs == null) return;

            foreach (var childrenRef in childrenRefs)
            {
                // looks up the element by its selfref in the elementLookup dictionary
                if (elementLookup.TryGetValue(childrenRef.Ref, out object element))
                {
                    //depending on element, fills out page info
                    if (element is TextItem textItem)
                    {
                        if (textItem.Prov != null && textItem.Prov.Any())
                        {
                            allPageElements.Add(new PageElementInfo
                            {
                                PageNo = textItem.Prov.FirstOrDefault().PageNo,
                                Top = textItem.Prov.FirstOrDefault().Bbox?.T ?? 0,
                                Content = ContentFormatter.ConvertEscapes(textItem.Text),
                                ElementType = textItem.Label
                            });
                        }
                    }//for TableItem
                    else if (element is TableItem tableItem)
                    {
                        if (tableItem.Prov != null && tableItem.Prov.Any())
                        {
                            string tableText = ContentFormatter.ConvertEscapes(ConvertTable(tableItem.Data)); //get rid of escapes and normalize text
                            allPageElements.Add(new PageElementInfo
                            {
                                PageNo = tableItem.Prov.FirstOrDefault().PageNo,
                                Top = tableItem.Prov.FirstOrDefault().Bbox?.T ?? 0,
                                Content = tableText,
                                ElementType = tableItem.Label
                            });
                        }
                    }//for PictureItem
                    else if (element is PictureItem pictureItem && fileType != ".pdf")
                    {
                        if (pictureItem.Prov != null && pictureItem.Prov.Any())
                        {
                            string imageDescription = "No description available";
                            if (pictureItem.Image.Uri != null)
                            {
                                imageDescription = await _vlmService.DescribeImageAsync(pictureItem.Image.Uri, "Describe the provided image in a clear and concise manner. Do not go off on a tangent for every detail spotted, just provide the main idea of the image. Limit to 1 sentence.");
                                _logger.LogInformation("Full image description received for page {PageNo}: {Description}", pictureItem.Prov.FirstOrDefault().PageNo, imageDescription);
                            }
                            allPageElements.Add(new PageElementInfo
                            {
                                PageNo = pictureItem.Prov.FirstOrDefault().PageNo,
                                Top = pictureItem.Prov.FirstOrDefault().Bbox?.T ?? 0,
                                Content = $"Image Description: {imageDescription}",
                                ElementType = pictureItem.Label
                            });
                        }
                    }//for PictureItem

                    else if (element is GroupItem groupItem)
                    {
                        await TraverseAndCollect(groupItem.Children);
                    }//traverse if Groupitem
                }//take element and fill get its page info
            }//traverse through all children


        }//define recursive Traverse function

        if (json_content.Body?.Children != null)
        {
            await TraverseAndCollect(json_content.Body.Children);
        }//call TraverseAndCollect to start


        if (fileType == ".pdf" && md_content != null)
        {
            //regex to find image URIs in the markdown content
            var reg = new Regex(@"(?<=\[Image\]\().*?(?=\))");
            //splits md content by page
            string[] splits = md_content.Split(new string[] { "[PAGE BREAK]" }, StringSplitOptions.None);

            int pageNo = 0;
            foreach (var page in splits)
            {
                pageNo += 1;
                var matches = reg.Matches(page);
                if (matches.Count > 0)
                {
                    foreach (Match match in matches)
                    {
                        var imageUri = match.Value;
                        var imageDescription = "No description available";
                        if (imageUri != null)
                        {
                            imageDescription = await _vlmService.DescribeImageAsync(imageUri, "Describe the provided image in a clear and concise manner. Do not go off on a tangent for every detail spotted, just provide the main idea of the image. Limit to 1 sentence.");

                            _logger.LogInformation("Full image description received for page {PageNo}: {Description}", pageNo, imageDescription); //page.PageNo, imageDescription);
                        }//get image description for each page in PDF, as it is stored in the page details

                        allPageElements.Add(new PageElementInfo
                        {
                            PageNo = pageNo,
                            Top = 0, //top is not used for PDF pages, so set to 0
                            Content = $"Image on page {pageNo}: {imageDescription}",
                            ElementType = "Page Image"
                        });//add page info to allPageElements

                    }//collect image details for each page in PDF

                }//makes sure image uris are not null

            }//goes through each page element (string), finds regex matches to get image uris, then adds to pageElement dictionary

        }//collect image information through markdown content, as json returns uris of the full page sometimes, leading to inaccurate image description


        //take current allPageElements, and temporarily aggregate all elements into dictionary with page numbers
        Dictionary<int, List<PageElementInfo>> temporaryPages = new Dictionary<int, List<PageElementInfo>>();

        foreach (var item in allPageElements)
        {
            int pageNumber = (int)item.PageNo;

            if (temporaryPages.ContainsKey(pageNumber))
            {
                temporaryPages[pageNumber].Add(item);
            }//if page number list already exists
            else
            {
                List<PageElementInfo> newPageList = new List<PageElementInfo>();
                newPageList.Add(item);
                temporaryPages.Add(pageNumber, newPageList);
            }//create new list for specific page number if doesn't exist

        }//add to temporaryPages to group elements by page

        List<int> sortedPageNumbers = new List<int>(temporaryPages.Keys);//sort by pages
        sortedPageNumbers.Sort();
        foreach (int pageNumber in sortedPageNumbers)
        {
            var pageTextBuilder = new StringBuilder();
            List<PageElementInfo> pageElements = temporaryPages[pageNumber];

            if (fileType == ".pptx")
            {
                pageElements.Sort((e1, e2) => e1.Top.CompareTo(e2.Top));
            }//for pptx the way the origin is set, the bounding box "top" element is reversed, so lower elements have a higher top value
            else
            {
                pageElements.Sort((e1, e2) => -e1.Top.CompareTo(e2.Top));
            }//for pdfs: sort by top coord, uses lambda, to have page structure. - sign to reverse order, as sorts from lowest "top" to highest



            foreach (var element in pageElements)
            {
                pageTextBuilder.AppendLine($"{element.Content}\n");
            }
            pageContent[pageNumber] = pageTextBuilder;
        }//go in order to aggregate content in different pages

        Dictionary<int, string> finalPageContents = new Dictionary<int, string>();
        foreach (var page in pageContent)
        {
            finalPageContents[page.Key] = ContentFormatter.ConvertEscapes(page.Value.ToString());
        }//convert StringBUilder from each page to actual string


        _logger.LogInformation("Finished generating");

        return finalPageContents;

    }//end of BuildPageContent

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
