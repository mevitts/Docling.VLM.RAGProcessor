namespace TestDocling.Services;

//extension methods for IServiceCollection to register Docling and Ollama services
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDoclingServices(this IServiceCollection services)
    {
        services.AddHttpClient("DoclingClient", client =>
        {
            string doclingURL = Environment.GetEnvironmentVariable("DOCLING_URL") ?? "http://localhost:5001";
            client.Timeout = TimeSpan.FromMinutes(5);
            client.BaseAddress = new Uri(doclingURL);
        });
        services.AddScoped<IDoclingContentProcessorService, DoclingContentProcessorService>();
        return services;
    }

    public static IServiceCollection AddOllamaServices(this IServiceCollection services)
    {
        services.AddHttpClient("OllamaClient", client =>
        {
            string ollamaURL = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434";
            client.Timeout = TimeSpan.FromMinutes(5);
            client.BaseAddress = new Uri(ollamaURL);
        });
        services.AddScoped<IVlmService, OllamaSharpVlmService>();
        return services;
    }
}
