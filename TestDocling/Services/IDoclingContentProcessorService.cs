namespace TestDocling.Services;

using TestDocling.Models;
using static TestDocling.Services.DoclingContentProcessorService;

public interface IDoclingContentProcessorService
{
    //process JSON string from Docling-serve, creates final page contents
    Task<Dictionary<int, PageOutput>> ProcessDoclingResponse(string doclingJsonOutput);
}

