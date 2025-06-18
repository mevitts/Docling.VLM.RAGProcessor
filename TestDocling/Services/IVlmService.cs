namespace TestDocling.Services;

public interface IVlmService
{
    Task<string> DescribeImageAsync(string uri, string prompt);
}
