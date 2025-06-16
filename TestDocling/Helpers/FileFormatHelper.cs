using Microsoft.AspNetCore.Mvc;

//gets file extension from file name so it matches Docling input file expectations
public static class FileFormatHelper
{

    private static readonly Dictionary<string, string> _formatMap = new()
{
    { "pdf", "pdf" },
    { "docx", "docx" },
    { "pptx", "pptx" },
    { "html", "html" },
    { "jpg", "image" },
    { "jpeg", "image" },
    { "png", "image" },
    { "csv", "csv" },
    { "xlsx", "xlsx" },
    { "xml", "xml_jats" },
    { "md", "md" }
};
    public static bool TryGetDoclingFormat(string fileName, out string fromFormat)
    {
        string? ext = Path.GetExtension(fileName)?.TrimStart('.').ToLower();
        if (ext != null && _formatMap.TryGetValue(ext, out fromFormat))
            return true;

        fromFormat = string.Empty;
        return false;
    }
}