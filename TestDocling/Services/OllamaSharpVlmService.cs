using Microsoft.AspNetCore.Http.HttpResults;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System;
using System.Runtime.Intrinsics.X86;
using System.Text;
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

    }//constructor to accept IHttpClientFactory and ILogger so can use them to make HTTP requests to Ollama and log information
    public async Task<string> DescribeImageAsync(string uri, string prompt)
    {
        var chat = new Chat(_ollamaApiClient);
        //chat.Model = "llava:7b"; // Set the model to use for image description

        try
        {
            uri = uri.Replace("data:image/png;base64,", ""); //remove base64 prefix if exists
            string image_url = $"data:image/png;base64,{uri}"; //convert uri to base64 image format for VLM
            _logger.LogInformation("Describing image with Ollama API: {ImageUrl}", image_url.Substring(0, 50));


            var responseBuilder = new StringBuilder();
            await foreach (var responsePart in chat.SendAsAsync(ChatRole.User, prompt, null, new List<string> { uri }, null))
            {
                _logger.LogInformation("Received response part: {ResponsePart}", responsePart);
                responseBuilder.Append(responsePart);
            }//continuously append response parts to the StringBuilder

            return responseBuilder.ToString();
        }//try block to get image description from Ollama
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error describing image with Ollama API: {Message}", ex.Message);
            return $"Error: {ex.Message}"; // Return error message if something goes wrong
        }//try + catch block for image description error handling
    }
}
