using TestDocling.Models;

namespace TestDocling.Services;

public interface IVlmService
{
    Task<ImageOutput> DescribeImageAsync(string uri, string prompt, int pageNo);
}
