using OpenAI.Chat;
using OpenAI;
using TestDocling.Services;

var builder = WebApplication.CreateBuilder(args);

var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];
if (string.IsNullOrEmpty(openAiApiKey))
{
    throw new InvalidOperationException("OpenAI API key is not configured.");
}

builder.Services.AddDoclingServices();
builder.Services.AddScoped<IDoclingContentProcessorService, DoclingContentProcessorService>();
builder.Services.AddScoped<IVlmService, OpenAiVlmService>();
builder.Services.AddSingleton(new ChatClient(model: "gpt-4o", apiKey: openAiApiKey));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.WebHost.UseUrls("http://0.0.0.0:80");

var app = builder.Build();

try
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
catch (Exception ex)
{
    Console.WriteLine($"Swagger failed: {ex.Message}");
}

app.MapControllers();

app.Run();
