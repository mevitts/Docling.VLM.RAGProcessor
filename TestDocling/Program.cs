var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
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
