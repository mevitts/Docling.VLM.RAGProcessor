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
                ChatMessageContentPart.CreateImagePart(new Uri(imageUri)),
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
            throw;
        }
    }
}
