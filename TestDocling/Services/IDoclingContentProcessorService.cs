namespace TestDocling.Services;

public interface IDoclingContentProcessorService
{
    //process JSON string from Docling-serve, creates final page contents
    Task<Dictionary<int, string>> ProcessDoclingResponse(string doclingJsonOutput);
}

