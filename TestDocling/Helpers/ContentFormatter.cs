namespace TestDocling.Helpers;


public static class ContentFormatter
{
    //Replaces newline ('\n') and tab ('\t') in strings with actual newline and tab

    public static string ConvertEscapes(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        content = content.Replace("\\n", "\n");
        content = content.Replace("\\t", "\t");

        return content;
    }
}
