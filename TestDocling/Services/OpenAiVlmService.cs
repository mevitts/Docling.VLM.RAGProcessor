using Azure;
using OpenAI;
using OpenAI.Assistants;
using OpenAI.Chat;
using System.ClientModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using TestDocling.Models;
using TestDocling.Services;

namespace TestDocling.Services;

public class OpenAiVlmService : IVlmService
{
    private readonly ChatClient _client;
    private readonly ILogger<OpenAiVlmService> _logger;

    public OpenAiVlmService(ChatClient client, ILogger<OpenAiVlmService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<ImageOutput> DescribeImageAsync(string imageUri, string prompt, int pageNo)
    {
        if (string.IsNullOrWhiteSpace(imageUri))
        {
            throw new ArgumentNullException(nameof(imageUri));
        }

        //extract raw image data and convert to bytes to feed, avoids errors thrown from URI length being too long.
        var parts = imageUri.Split(',');
        if (parts.Length != 2)
        {
            throw new ArgumentException("Invalid Data URI format.", nameof(imageUri));
        }
        string mimeType = parts[0].Split(';')[0].Split(':')[1];

        var imageBytes = Convert.FromBase64String(parts[1]);
        var binaryImageData = BinaryData.FromBytes(imageBytes);


        string pageNoString = pageNo.ToString();
        var imageOutputSchema = BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "title": { "type": "string", "description": "A descriptive title for the image" },
                    "description": { "type": "string", "description": "A detailed description of the image's content" }
                },
                "required": ["title", "description"],
                "additionalProperties": false 
            }
            """);

        List<OpenAI.Chat.ChatMessage> messages =
        [
            new UserChatMessage(new List<ChatMessageContentPart>
            {
                ChatMessageContentPart.CreateTextPart(prompt),
                ChatMessageContentPart.CreateImagePart(binaryImageData, mimeType),
            })
        ];

        var options = new ChatCompletionOptions()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: pageNoString,
                jsonSchema: imageOutputSchema,
                jsonSchemaIsStrict: true)
        };

        try
        {
            ChatCompletion chatCompletion = await _client.CompleteChatAsync(messages, options); ;
            string jsonResponse = chatCompletion.Content[0].Text;
            _logger.LogInformation("Received structured JSON from OpenAI: {JsonResponse}", jsonResponse);

            var imageOutput = JsonSerializer.Deserialize<ImageOutput>(jsonResponse);
            return imageOutput;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API call failed for page {PageNo}. Error: {ErrorMessage}", pageNo, ex.Message);

            throw;
        }
    }
}
