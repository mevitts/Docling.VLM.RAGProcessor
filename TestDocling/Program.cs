using TestDocling.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDoclingServices();
builder.Services.AddOllamaServices();
builder.Services.AddScoped<IDoclingContentProcessorService, DoclingContentProcessorService>();
builder.Services.AddScoped<IVlmService, OllamaSharpVlmService>(); 
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
