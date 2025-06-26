using Microsoft.AspNetCore.Http.HttpResults;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
using TestDocling.Models;
using static System.Net.WebRequestMethods;

namespace TestDocling.Services;

public class OllamaSharpVlmService : IVlmService
{
    private readonly OllamaApiClient _ollamaApiClient;
    private readonly ILogger<OllamaSharpVlmService> _logger;

    public OllamaSharpVlmService(IHttpClientFactory httpClientFactory, ILogger<OllamaSharpVlmService> logger)
    {
        var httpClient = httpClientFactory.CreateClient("OllamaClient");
        _ollamaApiClient = new OllamaApiClient(httpClient);
        _ollamaApiClient.SelectedModel = "llava:7b";
        _logger = logger;

    }
    public async Task<ImageOutput> DescribeImageAsync(string uri, string prompt, int pageNo)
    {
        var chat = new Chat(_ollamaApiClient);

        try
        {
            //sets universal prefix for uris: if the uri already has it, will replace then add it back
            uri = uri.Replace("data:image/png;base64,", "");
            string image_url = $"data:image/png;base64,{uri}";

            var responseBuilder = new StringBuilder();
            await foreach (var responsePart in chat.SendAsAsync(ChatRole.User, prompt, null, new List<string> { uri }, null))
            {
                _logger.LogInformation("Received response part: {ResponsePart}", responsePart);
                responseBuilder.Append(responsePart);
            }

            var imageOutput = JsonSerializer.Deserialize<ImageOutput>(responseBuilder.ToString());
            return imageOutput;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error describing image with Ollama API: {Message}", ex.Message);
            throw;
        }
    }
}
