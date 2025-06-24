using TestDocling.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();

//register httpClient so Docling Controller can use and call docling-serve
builder.Services.AddHttpClient("DoclingClient", client =>
{
    string doclingURL = Environment.GetEnvironmentVariable("DOCLING_URL") ?? "http://localhost:5001";
    client.Timeout = TimeSpan.FromMinutes(5);
    client.BaseAddress = new Uri(doclingURL);
});

//register httpClient so can use and call OllamaClient
builder.Services.AddHttpClient("OllamaClient", client =>
{
    string ollamaURL = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434";
    client.Timeout = TimeSpan.FromMinutes(5);
    client.BaseAddress = new Uri(ollamaURL);
});
//register DoclingContentProcessorService interface and implementation, new instance created per HTTP request.
builder.Services.AddScoped<IDoclingContentProcessorService, DoclingContentProcessorService>();

//register Ollama Service implementation
builder.Services.AddScoped<IVlmService, OllamaSharpVlmService>(); 


builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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
